# Design Document — Customer Payment Flow

## Overview

This document describes the technical design of the complete customer payment flow in FashionHub, an ASP.NET Core 9 e-commerce API. The flow covers everything from cart management through Stripe payment intent creation, webhook-driven order creation, stock decrement, and order retrieval.

The architecture follows a clean layered structure:

```
Presentation (Controllers)
    ↓
Application (Services + DTOs)
    ↓
Domain (Entities + Repository Interfaces)
    ↓
Infrastructure (EF Core + Repository Implementations)
```

---

## Architecture Overview

### Layer Responsibilities

| Layer | Components | Responsibility |
|---|---|---|
| **Presentation** | `PaymentController`, `CartController`, `OrdersController` | HTTP routing, auth claims extraction, response shaping |
| **Application** | `PaymentService`, `CartService`, `OrderService` | Business logic, orchestration, DTO mapping |
| **Domain** | `Payment`, `Order`, `Cart`, `Product`, `OrderItem`, `CartItem` | Entity definitions, repository interfaces |
| **Infrastructure** | `PaymentRepository`, `CartRepository`, `OrderRepository`, `UnitOfWork` | EF Core queries, data persistence |

### Cross-Cutting Concerns

- **Authentication**: JWT Bearer tokens. `customerId` is extracted from the `ClaimTypes.NameIdentifier` claim.
- **Result Pattern**: All service methods return `ServiceResult<T>` — a wrapper with `IsSuccess`, `Data`, and `Errors` fields. Controllers never throw; they inspect `IsSuccess` and return the appropriate HTTP status.
- **Unit of Work**: `IUnitOfWork` aggregates all repositories and exposes a single `SaveChangesAsync()`. All writes in a single business operation go through one `SaveChangesAsync` call to maintain atomicity.
- **Soft Delete**: Entities inherit from `BaseEntity<T>` which includes an `IsDeleted` flag. Queries filter on `!IsDeleted`.

---

## Component Design

### PaymentController

**Route**: `api/payment`

| Endpoint | Method | Auth | Description |
|---|---|---|---|
| `/create-payment-intent` | POST | `[Authorize]` | Initiates a Stripe payment session |
| `/webhook` | POST | `[AllowAnonymous]` | Receives Stripe webhook events |

**`CreatePaymentIntent` flow:**
1. Extract `customerId` from `ClaimTypes.NameIdentifier`. Return 401 if missing.
2. Call `IPaymentService.CreatePaymentIntentAsync(dto, customerId)`.
3. Return `Ok(result)` on success, `BadRequest(result)` on failure.

**`StripeWebhook` flow:**
1. Read `Stripe:WebhookSecret` from configuration. Return 500 if not configured.
2. Read raw request body using `StreamReader`.
3. Call `EventUtility.ConstructEvent(json, Stripe-Signature header, webhookSecret)`. Return 400 on `StripeException`.
4. Log event type and ID at Information level.
5. Switch on `stripeEvent.Type`:
   - `payment_intent.succeeded` → call `HandlePaymentSucceededAsync`
   - `payment_intent.payment_failed` → call `HandlePaymentFailedAsync`
   - default → log unhandled event type
6. **Always return HTTP 200** regardless of handler result (prevents Stripe retries).

---

### CartController

**Route**: `api/cart`  
**Auth**: `[Authorize]` on all endpoints (class-level)

All endpoints extract `userId` from `ClaimTypes.NameIdentifier` and delegate to `ICartService`.

| Endpoint | Method | Service Method |
|---|---|---|
| `/` | GET | `GetCartAsync` |
| `/items` | POST | `AddToCartAsync` |
| `/items` | PUT | `UpdateCartItemQuantityAsync` |
| `/items/{id}/increase` | PUT | `IncreaseCartItemQuantityAsync` |
| `/items/{id}/decrease` | PUT | `DecreaseCartItemQuantityAsync` |
| `/items/{id}` | DELETE | `RemoveCartItemAsync` |
| `/clear` | DELETE | `ClearCartAsync` |
| `/count` | GET | `GetCartItemCountAsync` |
| `/check-product/{productId}` | GET | `IsProductInCartAsync` |

---

### OrdersController

**Route**: `api/orders`

| Endpoint | Method | Auth | Service Method |
|---|---|---|---|
| `/` | POST | `[Authorize(Roles="Customer")]` | `CreateOrderAsync` |
| `/{id}` | GET | `[Authorize]` | `GetOrderByIdAsync` |
| `/` | GET | `[Authorize(Roles="Customer")]` | `GetUserOrdersAsync` |
| `/{id}/status` | PUT | `[Authorize(Roles="Admin")]` | `UpdateOrderStatusAsync` |
| `/admin` | GET | `[Authorize(Roles="Admin")]` | `GetOrdersAsync` |

> Note: In the payment-driven flow, orders are created automatically by the webhook handler. The `POST /orders` endpoint exists for manual order creation but is not part of the primary payment flow.

---

### PaymentService

**Interface**: `IPaymentService`

#### `CreatePaymentIntentAsync(dto, customerId)`

```
1. GetCartWithItemsByCustomerIdAsync(customerId)
   → Failure("Cart not found") if null
   → Failure("Cart is empty") if no CartItems

2. totalAmount = Σ (CartItem.Quantity × CartItem.PriceAtAddition)

3. Stripe PaymentIntentCreateOptions:
   - Amount = (long)(totalAmount × 100)   // cents
   - Currency = "usd"
   - PaymentMethodTypes = ["card"]
   - Metadata = { "customerId": customerId }

4. paymentIntentService.CreateAsync(options)
   → StripeException → Failure("Stripe error: {message}")
   → Exception       → Failure("Error creating payment intent: {message}")

5. Persist Payment {
     Amount = totalAmount,
     StripePaymentIntentId = paymentIntent.Id,
     Status = "pending",
     CustomerId = customerId
   }

6. SaveChangesAsync()

7. Return PaymentIntentResponseDto {
     ClientSecret = paymentIntent.ClientSecret,
     Amount = totalAmount
   }
```

#### `HandlePaymentSucceededAsync(paymentIntentId)`

```
1. GetByPaymentIntentIdAsync(paymentIntentId)
   → Failure("Payment record not found") if null

2. Idempotency check:
   if payment.Status == "succeeded" → Success(0)  // already processed

3. payment.Status = "succeeded"
   payment.PaymentDate = DateTime.UtcNow

4. GetCartWithItemsByCustomerIdAsync(payment.CustomerId)
   → Failure("Cart not found or already cleared") if null or empty

5. Build Order {
     CustomerId  = payment.CustomerId,
     TotalAmount = payment.Amount,
     Status      = OrderStatus.Processing,
     Payment     = payment
   }

6. For each CartItem:
   - Add OrderItem {
       ProductId, ProductName, ProductSKU,
       UnitPrice = CartItem.PriceAtAddition,
       Quantity, Subtotal = Quantity × UnitPrice,
       SelectedSize, SelectedColor
     }
   - product.StockQuantity -= cartItem.Quantity

7. Orders.AddAsync(order)
8. Carts.ClearCartAsync(cart.Id)
9. SaveChangesAsync()   ← single atomic commit

10. Return Success(order.Id)
```

#### `HandlePaymentFailedAsync(paymentIntentId)`

```
1. GetByPaymentIntentIdAsync(paymentIntentId)
   → Failure("Payment record not found") if null

2. payment.Status = "failed"
3. SaveChangesAsync()
4. Return Success(true)
   // Cart is NOT cleared — customer can retry
```

---

### CartService

All methods follow the same pattern:
1. Resolve `Customer` from `userId` via `IUnitOfWork.Users.GetCustomerByUserIdAsync(userId)`.
2. Get or create the cart via `IUnitOfWork.Carts.GetOrCreateCartAsync(customer.Id)`.
3. Validate ownership: `cartItem.CartId != cart.Id` → Failure.
4. Delegate to the repository method.
5. Call `SaveChangesAsync()`.
6. Reload and return the updated `CartDto`.

**Key stock validation points:**
- `AddToCartAsync`: checks `product.StockQuantity >= request.Quantity` before adding.
- `IncreaseCartItemQuantityAsync`: delegates to `CartRepository.IncreaseCartItemQuantityAsync` which checks `product.StockQuantity > cartItem.Quantity`.
- `DecreaseCartItemQuantityAsync`: if `quantity == 1`, the item is removed instead of decremented.

**`PriceAtAddition` logic** (in `CartRepository.AddItemToCartAsync`):
```csharp
PriceAtAddition = product.IsOnSale && product.DiscountPrice.HasValue
    ? product.DiscountPrice.Value
    : product.Price
```
The price is locked at the time of cart addition, not at checkout.

---

### OrderService

- `GetUserOrdersAsync`: returns all orders for the authenticated customer via `GetCustomerOrdersAsync(userId)`.
- `GetOrderByIdAsync`: verifies ownership — if `order.CustomerId != userId`, checks if the user is an Admin before allowing access.
- `UpdateOrderStatusAsync`: validates the caller is an Admin before updating `order.Status`.
- `GetOrdersAsync`: paginated query with `OrderByDescending(o => o.OrderDate)`, supports optional `status` filter.

---

## Data Models

### Entity Relationships

```
AppUser (ASP.NET Identity)
  └── Customer (1:1)
        ├── Cart (1:1)
        │     └── CartItem[] (1:N)
        │           └── Product (N:1)
        └── Order[] (1:N)
              ├── OrderItem[] (1:N)
              │     └── Product (N:1)
              └── Payment (1:1)
```

### Key Entity Fields

**Payment**
```csharp
int    Id
decimal Amount
string StripePaymentIntentId   // Stripe's PI id, used as lookup key
string Status                  // "pending" | "succeeded" | "failed"
DateTime PaymentDate
string CustomerId              // FK to Customer, stored at intent creation
```

**Order**
```csharp
int         Id
string      CustomerId
DateTime    OrderDate
OrderStatus Status             // Pending, Processing, Shipped, Delivered, Cancelled, Returned
decimal     TotalAmount
string      OrderNotes
int?        PaymentId          // FK to Payment
```

**OrderItem**
```csharp
int     Id
int     OrderId
int     ProductId
string  ProductName
string  ProductSKU
decimal UnitPrice              // = CartItem.PriceAtAddition at time of purchase
int     Quantity
decimal Subtotal               // = Quantity × UnitPrice
string  SelectedSize
string  SelectedColor
```

**Cart**
```csharp
int      Id
string   CustomerId
DateTime CreatedAt
DateTime ModifiedAt
```

**CartItem**
```csharp
int     Id
int     CartId
int     ProductId
int     Quantity
decimal PriceAtAddition        // locked at time of cart addition
string  SelectedSize
string  SelectedColor
```

---

## DTOs

### Request DTOs

**`CreatePaymentIntentDto`**
```csharp
int CartId   // accepted but ignored — cart resolved from JWT customerId
```

**`AddToCartDto`**
```csharp
int    ProductId
int    Quantity
string SelectedSize
string SelectedColor
```

**`UpdateCartItemDto`**
```csharp
int CartItemId
int Quantity
```

**`CreateOrderDto`**
```csharp
int    CartId
string OrderNotes
```

**`UpdateOrderStatusDto`**
```csharp
OrderStatus Status
```

### Response DTOs

**`PaymentIntentResponseDto`**
```csharp
string  ClientSecret   // passed to Stripe.js on the frontend
decimal Amount         // in dollars (not cents)
```

**`CartDto`**
```csharp
int           Id
string        CustomerId
DateTime      CreatedAt
DateTime      ModifiedAt
List<CartItemDto> Items
decimal       TotalPrice    // computed: Σ (UnitPrice × Quantity)
int           TotalItems    // computed: Σ Quantity
```

**`CartItemDto`**
```csharp
int     Id
int     ProductId
string  ProductName
string  ProductImageUrl
int     Quantity
decimal UnitPrice
decimal TotalPrice          // computed: UnitPrice × Quantity
string  SelectedSize
string  SelectedColor
```

**`OrderDto`**
```csharp
string          CustomerName
DateTime        OrderDate
OrderStatus     Status
decimal         TotalAmount
string          OrderNotes
List<OrderItemDto> OrderItems
```

**`OrderItemDto`**
```csharp
int     ProductId
string  ProductName
decimal UnitPrice
int     Quantity
decimal Subtotal
string  ProductSKU
string  SelectedSize
string  SelectedColor
```

---

## Complete Customer Payment Flow

### Sequence Diagram

```
Customer          CartController      CartService        CartRepository       Stripe           PaymentController    PaymentService      OrderRepository
   |                    |                  |                   |                 |                    |                   |                   |
   |-- GET /cart ------>|                  |                   |                 |                    |                   |                   |
   |                    |-- GetCartAsync ->|                   |                 |                    |                   |                   |
   |                    |                  |-- GetOrCreate --->|                 |                    |                   |                   |
   |                    |                  |<-- CartDto -------|                 |                    |                   |                   |
   |<-- CartDto --------|                  |                   |                 |                    |                   |                   |
   |                    |                  |                   |                 |                    |                   |                   |
   |-- POST /cart/items>|                  |                   |                 |                    |                   |                   |
   |                    |-- AddToCart ---->|                   |                 |                    |                   |                   |
   |                    |                  |-- StockCheck ---->|                 |                    |                   |                   |
   |                    |                  |-- AddItem ------->|                 |                    |                   |                   |
   |<-- CartDto --------|                  |                   |                 |                    |                   |                   |
   |                    |                  |                   |                 |                    |                   |                   |
   |-- POST /payment/create-payment-intent (JWT) ------------>|                 |                    |                   |                   |
   |                    |                  |                   |                 |                    |-- CreateIntent --->|                   |
   |                    |                  |                   |                 |                    |                   |-- GetCart -------->|
   |                    |                  |                   |                 |                    |                   |<-- Cart -----------|
   |                    |                  |                   |                 |                    |                   |                   |
   |                    |                  |                   |                 |<-- CreateIntent ---|                   |                   |
   |                    |                  |                   |                 |-- PaymentIntent --->                   |                   |
   |                    |                  |                   |                 |                    |                   |-- SavePayment ---->|
   |<-- { clientSecret, amount } ------------------------------------------------------------------------------------------------           |
   |                    |                  |                   |                 |                    |                   |                   |
   |-- Stripe.js confirms payment on frontend                  |                 |                    |                   |                   |
   |                    |                  |                   |                 |                    |                   |                   |
   |                    |                  |                   |   Stripe -----> POST /payment/webhook|                   |                   |
   |                    |                  |                   |                 |                    |-- VerifySignature  |                   |
   |                    |                  |                   |                 |                    |-- HandleSucceeded->|                   |
   |                    |                  |                   |                 |                    |                   |-- GetPayment ----->|
   |                    |                  |                   |                 |                    |                   |-- GetCart -------->|
   |                    |                  |                   |                 |                    |                   |-- CreateOrder ---->|
   |                    |                  |                   |                 |                    |                   |-- DecrStock ------>|
   |                    |                  |                   |                 |                    |                   |-- ClearCart ------>|
   |                    |                  |                   |                 |                    |                   |-- SaveChanges ---->|
   |                    |                  |                   |                 |   <-- HTTP 200 ----|                   |                   |
   |                    |                  |                   |                 |                    |                   |                   |
   |-- GET /orders (JWT) ----------------------------------------------------------------->          |                   |                   |
   |<-- [ OrderDto ] -----------------------------------------------------------------------        |                   |                   |
```

### Step-by-Step Flow Summary

| Step | Actor | Action | Outcome |
|---|---|---|---|
| 1 | Customer | `GET /api/cart` | Retrieves or creates cart |
| 2 | Customer | `POST /api/cart/items` | Adds items; price locked at `PriceAtAddition` |
| 3 | Customer | `POST /api/payment/create-payment-intent` | Server creates Stripe PaymentIntent; saves `pending` Payment; returns `clientSecret` |
| 4 | Frontend | Stripe.js confirms payment using `clientSecret` | Payment processed by Stripe |
| 5 | Stripe | `POST /api/payment/webhook` (`payment_intent.succeeded`) | Server verifies signature; creates Order; decrements stock; clears cart |
| 6 | Customer | `GET /api/orders` | Retrieves created orders |

---

## Repository Design

### IPaymentRepository

```csharp
Task<Payment?> GetByPaymentIntentIdAsync(string paymentIntentId);
// + inherited GenericRepository: AddAsync, GetByIdAsync, SaveChangesAsync
```

### ICartRepository

```csharp
Task<Cart?> GetCartWithItemsByCustomerIdAsync(string customerId);
Task<Cart?> GetCartWithItemsByIdAsync(int id);
Task<Cart>  GetOrCreateCartAsync(string customerId);
Task<bool>  AddItemToCartAsync(int cartId, int productId, int quantity, string size, string color);
Task<bool>  UpdateCartItemQuantityAsync(int cartItemId, int quantity);
Task<bool>  RemoveCartItemAsync(int cartItemId);
Task<bool>  ClearCartAsync(int cartId);
Task<bool>  IncreaseCartItemQuantityAsync(int cartItemId);
Task<bool>  DecreaseCartItemQuantityAsync(int cartItemId);
Task<CartItem?> GetCartItemByIdAsync(int cartItemId);
Task<int>   GetCartItemCountAsync(string customerId);
Task<bool>  IsProductInCartAsync(string customerId, int productId);
```

### IOrderRepository

```csharp
Task<Order?> GetOrderWithItemsAsync(int orderId);
Task<List<Order>> GetCustomerOrdersAsync(string customerId);
Task<Order?> CreateOrderFromCartAsync(int cartId);
IQueryable<Order> GetAllQueryable();
// + inherited: AddAsync, GetByIdAsync
```

### UnitOfWork

```csharp
IUserRepository    Users
IProductRepository Products
ICartRepository    Carts
IOrderRepository   Orders
IPaymentRepository Payments
IGenericRepository<Admin>    Admins
IGenericRepository<Customer> Customers

Task<int> SaveChangesAsync()
```

---

## Error Handling Strategy

| Scenario | Layer | Response |
|---|---|---|
| Missing JWT | Middleware | HTTP 401 |
| Missing `customerId` claim | Controller | HTTP 401 "Customer ID not found" |
| Cart not found | Service | `ServiceResult.Failure` → HTTP 400 |
| Cart empty | Service | `ServiceResult.Failure` → HTTP 400 |
| Stripe exception | Service | `ServiceResult.Failure` → HTTP 400 |
| Webhook signature invalid | Controller | HTTP 400 |
| Webhook secret not configured | Controller | HTTP 500 |
| Payment record not found | Service | `ServiceResult.Failure` (logged, HTTP 200 to Stripe) |
| SaveChanges failure | Service | `ServiceResult.Failure` — no partial commit |
| Unauthorized order access | Service | `ServiceResult.Failure` → HTTP 404 |

---

## Security Design

### Authentication & Authorization

- All customer-facing endpoints require `[Authorize]`.
- The webhook endpoint uses `[AllowAnonymous]` — security is enforced via Stripe signature verification instead.
- Role-based access: `[Authorize(Roles="Customer")]` for order creation/retrieval, `[Authorize(Roles="Admin")]` for admin order management.

### Webhook Security

```
Stripe → POST /api/payment/webhook
         ↓
         Read raw body (StreamReader — must not be buffered)
         ↓
         EventUtility.ConstructEvent(json, Stripe-Signature, WebhookSecret)
         ↓
         StripeException → 400 Bad Request (signature mismatch)
         ↓
         Process event
```

The raw body must be read before any middleware touches it. The `Stripe-Signature` header contains a timestamp and HMAC signature that Stripe uses to prevent replay attacks.

### Idempotency

The webhook handler checks `payment.Status == "succeeded"` before processing. If already succeeded, it returns `Success(0)` immediately without creating a duplicate order or clearing the cart again.

---

## Configuration

**`appsettings.json` keys used by the payment flow:**

```json
{
  "Stripe": {
    "SecretKey": "sk_...",
    "WebhookSecret": "whsec_..."
  },
  "JWT": {
    "Key": "...",
    "Issuer": "...",
    "Audience": "...",
    "DurationInDays": 7
  }
}
```

**Stripe SDK initialization** (in `Program.cs`):
```csharp
StripeConfiguration.ApiKey = builder.Configuration["Stripe:SecretKey"];
```

---

## Correctness Properties

These are the formal properties the implementation must satisfy, suitable for property-based testing:

### P1 — Payment Amount Consistency
For any non-empty cart, the `PaymentIntent.Amount` sent to Stripe must equal `Σ (CartItem.Quantity × CartItem.PriceAtAddition) × 100` (in cents). The `PaymentIntentResponseDto.Amount` must equal the same sum in dollars.

### P2 — Payment Record Persistence
After a successful `CreatePaymentIntentAsync` call, exactly one `Payment` record must exist in the database with `Status = "pending"` and the correct `StripePaymentIntentId` and `CustomerId`.

### P3 — Order Creation on Webhook Success
After `HandlePaymentSucceededAsync` completes successfully, exactly one `Order` must exist with `Status = OrderStatus.Processing`, `TotalAmount = Payment.Amount`, and one `OrderItem` per `CartItem` with matching `ProductId`, `Quantity`, `UnitPrice`, and `Subtotal`.

### P4 — Stock Decrement Correctness
After `HandlePaymentSucceededAsync` completes, for each product in the order: `Product.StockQuantity_after = Product.StockQuantity_before - OrderItem.Quantity`.

### P5 — Cart Cleared After Success
After `HandlePaymentSucceededAsync` completes successfully, the customer's cart must contain zero `CartItem` records.

### P6 — Cart Preserved After Failure
After `HandlePaymentFailedAsync` completes, the customer's cart must contain the same `CartItem` records as before the call (no items removed, no quantities changed).

### P7 — Idempotency
Calling `HandlePaymentSucceededAsync` twice with the same `paymentIntentId` must result in exactly one `Order` and the cart being cleared only once. The second call must return `Success(0)` without side effects.

### P8 — Stock Validation Before Cart Addition
For any `AddToCartAsync` call where `request.Quantity > product.StockQuantity`, the service must return a failure result and the cart must remain unchanged.

### P9 — Price Lock
`CartItem.PriceAtAddition` must equal `product.DiscountPrice` if `product.IsOnSale && product.DiscountPrice.HasValue`, otherwise `product.Price`. This value must not change after the item is added to the cart, even if the product price changes later.

### P10 — Webhook Authorization
Any `POST /api/payment/webhook` request with an invalid or missing `Stripe-Signature` header must return HTTP 400 and must not trigger any database writes.
