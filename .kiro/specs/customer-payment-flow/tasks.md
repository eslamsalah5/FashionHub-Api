# Implementation Plan: Customer Payment Flow

## Overview

Implement the complete customer payment flow for FashionHub, covering cart management, Stripe payment intent creation, webhook-driven order creation, stock decrement, and order retrieval. The implementation follows the existing layered architecture (Presentation → Application → Domain → Infrastructure) using C# / ASP.NET Core 9, the Unit of Work + Repository pattern, and `ServiceResult<T>` for all service responses.

Most of the core code already exists. Tasks focus on gaps, hardening, and test coverage identified in the requirements and design documents.

---

## Tasks

- [x] 1. Harden `PaymentController` — authentication and webhook security
  - [x] 1.1 Enforce `customerId` null/empty guard in `CreatePaymentIntent`
    - In `PaymentController.CreatePaymentIntent`, verify the existing guard returns HTTP 401 with the exact message `"Customer ID not found"` when the `NameIdentifier` claim is missing or empty
    - Confirm `[Authorize]` attribute is present on the endpoint
    - _Requirements: 1.1, 1.3, 1.4, 8.5_

  - [x] 1.2 Enforce webhook secret configuration guard
    - In `PaymentController.StripeWebhook`, verify the existing guard returns HTTP 500 with `"Webhook secret not configured"` when `Stripe:WebhookSecret` is null, empty, or starts with `"whsec_your"`
    - Confirm `[AllowAnonymous]` attribute is present on the webhook endpoint
    - _Requirements: 4.1, 4.3, 8.4_

  - [x] 1.3 Write unit tests for `PaymentController` auth and webhook guards
    - Test: missing JWT → 401
    - Test: missing `customerId` claim → 401 "Customer ID not found"
    - Test: missing webhook secret → 500
    - Test: invalid Stripe signature → 400
    - _Requirements: 1.1, 1.4, 4.2, 4.3_

- [x] 2. Implement `PaymentService.CreatePaymentIntentAsync` — gaps and validation
  - [x] 2.1 Verify cart resolution uses JWT `customerId`, not DTO `CartId`
    - Confirm `CreatePaymentIntentAsync` ignores `dto.CartId` and resolves the cart via `customerId` from the JWT claim
    - Confirm the `totalAmount` calculation is `Σ (CartItem.Quantity × CartItem.PriceAtAddition)`
    - Confirm the Stripe `Amount` is `(long)(totalAmount × 100)` with `Currency = "usd"` and `PaymentMethodTypes = ["card"]`
    - Confirm `customerId` is stored in Stripe metadata under key `"customerId"`
    - _Requirements: 3.1, 3.4, 3.5, 3.6, 3.11_

  - [x] 2.2 Verify `Payment` record persistence with correct fields
    - Confirm a `Payment` record is saved with `Status = "pending"`, `StripePaymentIntentId`, `Amount = totalAmount`, and `CustomerId` before returning
    - Confirm `SaveChangesAsync` is called once after persisting the payment
    - _Requirements: 3.7, 3.8_

  - [x] 2.3 Write property test for P1 — Payment Amount Consistency
    - **Property P1: Payment Amount Consistency**
    - For any non-empty cart, assert `PaymentIntent.Amount == Σ(Quantity × PriceAtAddition) × 100` and `PaymentIntentResponseDto.Amount == Σ(Quantity × PriceAtAddition)`
    - Use `FsCheck` or `CsCheck` to generate arbitrary cart item collections with positive quantities and prices
    - **Validates: Requirements 3.4, 3.5**

  - [x] 2.4 Write property test for P2 — Payment Record Persistence
    - **Property P2: Payment Record Persistence**
    - After a successful `CreatePaymentIntentAsync`, assert exactly one `Payment` record exists with `Status = "pending"`, the correct `StripePaymentIntentId`, and the correct `CustomerId`
    - **Validates: Requirements 3.7**

  - [x] 2.5 Write unit tests for `CreatePaymentIntentAsync` failure paths
    - Test: cart not found → `Failure("Cart not found")`
    - Test: empty cart → `Failure("Cart is empty")`
    - Test: `StripeException` → `Failure("Stripe error: ...")`
    - Test: unexpected exception → `Failure("Error creating payment intent: ...")`
    - _Requirements: 3.2, 3.3, 3.9, 3.10_

- [x] 3. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 4. Implement `PaymentService.HandlePaymentSucceededAsync` — order creation and stock decrement
  - [x] 4.1 Verify idempotency guard
    - Confirm that when `payment.Status == "succeeded"`, the method returns `Success(0)` immediately without creating a new `Order` or clearing the cart
    - _Requirements: 4.6, 8.1, 8.2_

  - [x] 4.2 Verify atomic order creation, stock decrement, and cart clear
    - Confirm `payment.Status` is set to `"succeeded"` and `payment.PaymentDate` to `DateTime.UtcNow`
    - Confirm `Order` is created with `CustomerId`, `TotalAmount = payment.Amount`, `Status = OrderStatus.Processing`, and a reference to the `Payment` record
    - Confirm one `OrderItem` is created per `CartItem` with `ProductId`, `ProductName`, `ProductSKU`, `UnitPrice = CartItem.PriceAtAddition`, `Quantity`, `Subtotal = Quantity × UnitPrice`, `SelectedSize`, `SelectedColor`
    - Confirm each `Product.StockQuantity` is decremented by the corresponding `CartItem.Quantity`
    - Confirm `ClearCartAsync` is called on the customer's cart
    - Confirm all of the above are committed in a **single** `SaveChangesAsync` call
    - _Requirements: 4.7, 4.10, 4.11, 4.12, 4.13, 7.1, 7.4_

  - [x] 4.3 Write property test for P3 — Order Creation on Webhook Success
    - **Property P3: Order Creation on Webhook Success**
    - After `HandlePaymentSucceededAsync` succeeds, assert exactly one `Order` exists with `Status = OrderStatus.Processing`, `TotalAmount = Payment.Amount`, and one `OrderItem` per `CartItem` with matching `ProductId`, `Quantity`, `UnitPrice`, and `Subtotal`
    - **Validates: Requirements 4.10, 4.11**

  - [x] 4.4 Write property test for P4 — Stock Decrement Correctness
    - **Property P4: Stock Decrement Correctness**
    - After `HandlePaymentSucceededAsync` succeeds, for each product assert `StockQuantity_after == StockQuantity_before - OrderItem.Quantity`
    - Generate arbitrary carts with multiple products and varying quantities
    - **Validates: Requirements 4.12, 7.1**

  - [x] 4.5 Write property test for P5 — Cart Cleared After Success
    - **Property P5: Cart Cleared After Success**
    - After `HandlePaymentSucceededAsync` succeeds, assert the customer's cart contains zero `CartItem` records
    - **Validates: Requirements 4.13**

  - [x] 4.6 Write property test for P7 — Idempotency
    - **Property P7: Idempotency**
    - Calling `HandlePaymentSucceededAsync` twice with the same `paymentIntentId` must result in exactly one `Order`, the cart cleared only once, and the second call returning `Success(0)`
    - **Validates: Requirements 8.1, 8.2**

  - [x] 4.7 Write unit tests for `HandlePaymentSucceededAsync` failure paths
    - Test: payment record not found → `Failure("Payment record not found")`
    - Test: cart not found or empty at webhook time → `Failure("Cart not found or already cleared")`
    - _Requirements: 4.5, 4.9_

- [x] 5. Implement `PaymentService.HandlePaymentFailedAsync` — payment failure handling
  - [x] 5.1 Verify payment failure sets status and preserves cart
    - Confirm `payment.Status` is set to `"failed"` and `SaveChangesAsync` is called
    - Confirm the customer's cart is **not** cleared and no `StockQuantity` is decremented
    - _Requirements: 5.3, 5.4, 5.5_

  - [x] 5.2 Write property test for P6 — Cart Preserved After Failure
    - **Property P6: Cart Preserved After Failure**
    - After `HandlePaymentFailedAsync` completes, assert the customer's cart contains the same `CartItem` records as before (no items removed, no quantities changed)
    - **Validates: Requirements 5.4, 5.5**

  - [x] 5.3 Write unit tests for `HandlePaymentFailedAsync` failure paths
    - Test: payment record not found → `Failure("Payment record not found")`
    - Test: unexpected exception → `Failure("Error handling payment failed: ...")`
    - _Requirements: 5.2, 5.6_

- [x] 6. Checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Harden `CartService` — stock validation and ownership checks
  - [x] 7.1 Verify stock validation in `AddToCartAsync`
    - Confirm `AddToCartAsync` checks `product.StockQuantity >= request.Quantity` before adding and returns `Failure("Not enough stock available. Only {n} items left.")` when the check fails
    - Confirm the cart is unchanged when the check fails
    - _Requirements: 2.3, 7.2_

  - [x] 7.2 Verify stock validation in `IncreaseCartItemQuantityAsync`
    - Confirm `IncreaseCartItemQuantityAsync` returns `Failure("Cannot increase quantity. Not enough items available in stock.")` when `product.StockQuantity <= cartItem.Quantity`
    - _Requirements: 2.4, 7.3_

  - [x] 7.3 Verify cart item ownership check
    - Confirm all mutating cart operations (`UpdateCartItemQuantityAsync`, `RemoveCartItemAsync`, `IncreaseCartItemQuantityAsync`, `DecreaseCartItemQuantityAsync`) return `Failure("Cart item does not belong to your cart")` when the item's `CartId` does not match the authenticated customer's cart
    - _Requirements: 2.9_

  - [x] 7.4 Verify `DecreaseCartItemQuantityAsync` removes item when quantity is 1
    - Confirm that when `CartItem.Quantity == 1`, `DecreaseCartItemQuantityAsync` removes the item instead of decrementing
    - _Requirements: 2.6_

  - [x] 7.5 Verify auto-cart creation on first access
    - Confirm `GetCartAsync` and `AddToCartAsync` call `GetOrCreateCartAsync` and create a new cart when none exists for the customer
    - _Requirements: 2.10_

  - [x] 7.6 Write property test for P8 — Stock Validation Before Cart Addition
    - **Property P8: Stock Validation Before Cart Addition**
    - For any `AddToCartAsync` call where `request.Quantity > product.StockQuantity`, assert the service returns a failure result and the cart remains unchanged
    - **Validates: Requirements 2.3, 7.2**

  - [x] 7.7 Write property test for P9 — Price Lock
    - **Property P9: Price Lock**
    - Assert `CartItem.PriceAtAddition == product.DiscountPrice` when `product.IsOnSale && product.DiscountPrice.HasValue`, otherwise `CartItem.PriceAtAddition == product.Price`
    - Assert the value does not change after the item is added, even if the product price is updated
    - **Validates: Requirements 2.2**

  - [x] 7.8 Write unit tests for `CartService` operations
    - Test: `GetCartAsync` with valid customer returns cart DTO
    - Test: `AddToCartAsync` with valid product and stock adds item
    - Test: `RemoveCartItemAsync` removes correct item
    - Test: `ClearCartAsync` removes all items
    - Test: `GetCartItemCountAsync` returns correct total quantity
    - _Requirements: 2.1, 2.7, 2.8_

- [x] 8. Verify `OrderService` — order retrieval and access control
  - [x] 8.1 Verify customer order retrieval
    - Confirm `GetUserOrdersAsync` returns all orders for the authenticated `customerId`
    - Confirm `GetOrderByIdAsync` returns order details including all `OrderItem` records when the order belongs to the authenticated customer
    - _Requirements: 6.1, 6.2_

  - [x] 8.2 Verify order access control
    - Confirm `GetOrderByIdAsync` returns `Failure("Unauthorized access to order")` when the order does not belong to the customer and the user is not an Admin
    - Confirm `UpdateOrderStatusAsync` returns `Failure("Unauthorized. Only administrators can update order status")` when called by a non-Admin
    - _Requirements: 6.3, 6.7_

  - [x] 8.3 Verify `OrderDto` fields
    - Confirm the returned `OrderDto` includes `CustomerName`, `OrderDate`, `Status`, `TotalAmount`, `OrderNotes`, and a populated list of `OrderItemDto`
    - _Requirements: 6.4_

  - [x] 8.4 Verify admin order management
    - Confirm `GetOrdersAsync` returns a paginated list of all orders ordered by `OrderDate` descending, supporting `page` and `pageSize` parameters
    - Confirm `UpdateOrderStatusAsync` updates `order.Status` to the provided `OrderStatus` value when called by an Admin
    - _Requirements: 6.5, 6.6_

  - [x] 8.5 Write unit tests for `OrderService`
    - Test: `GetUserOrdersAsync` returns correct orders for customer
    - Test: `GetOrderByIdAsync` returns 404-equivalent failure for wrong customer
    - Test: `UpdateOrderStatusAsync` succeeds for Admin, fails for non-Admin
    - Test: `GetOrdersAsync` returns paginated results
    - _Requirements: 6.1, 6.2, 6.3, 6.5, 6.6, 6.7_

- [x] 9. Verify logging in `PaymentController`
  - [x] 9.1 Confirm all required log statements are present
    - Verify `Information` log on webhook receipt: `"Stripe webhook received: {EventType} | {EventId}"`
    - Verify `Warning` log on signature verification failure
    - Verify `Error` log when `HandlePaymentSucceededAsync` fails, including `paymentIntentId` and error messages
    - Verify `Error` log when `HandlePaymentFailedAsync` fails, including `paymentIntentId` and error messages
    - Verify `Error` log when `Stripe:WebhookSecret` is not configured
    - Verify `Information` log for unhandled Stripe event types: `"Unhandled Stripe event type: {EventType}"`
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.6_

  - [x] 9.2 Write unit tests for `PaymentController` logging
    - Test: webhook receipt logs event type and ID at Information level
    - Test: invalid signature logs at Warning level
    - Test: failed `HandlePaymentSucceededAsync` logs at Error level with `paymentIntentId`
    - Test: unconfigured webhook secret logs at Error level
    - _Requirements: 9.1, 9.2, 9.3, 9.5_

- [x] 10. Verify webhook security — signature and `[AllowAnonymous]`
  - [x] 10.1 Confirm raw body reading and signature verification
    - Confirm the webhook endpoint reads the raw request body via `StreamReader` before any middleware buffering
    - Confirm `EventUtility.ConstructEvent` is called with the raw body, `Stripe-Signature` header, and `WebhookSecret`
    - Confirm a `StripeException` during construction returns HTTP 400
    - _Requirements: 4.1, 4.2, 8.3_

  - [x] 10.2 Write property test for P10 — Webhook Authorization
    - **Property P10: Webhook Authorization**
    - For any `POST /api/payment/webhook` request with an invalid or missing `Stripe-Signature` header, assert the response is HTTP 400 and no database writes occurred
    - **Validates: Requirements 4.1, 4.2, 8.3**

- [x] 11. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

---

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP delivery
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation at logical boundaries
- Property tests (P1–P10) validate universal correctness properties defined in the design document
- Unit tests validate specific examples, edge cases, and error conditions
- All service methods use `ServiceResult<T>` — never throw from service layer
- All writes within a single business operation must go through a single `SaveChangesAsync` call (atomicity)
- Recommended property-based testing libraries for C#: `FsCheck` (NuGet) or `CsCheck` (NuGet)
