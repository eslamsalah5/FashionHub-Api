# Requirements Document

## Introduction

This document outlines the requirements for the complete Customer Payment Flow in the FashionHub project — a fashion e-commerce API built with ASP.NET Core 9. It covers every step from the moment a customer initiates checkout through payment confirmation and order creation, including cart management, payment intent creation via Stripe, webhook processing, and stock updates.

The current flow relies on:
- **Stripe Payment Intents API** for payment processing
- **Stripe Webhooks** for confirming payment success or failure
- **JWT Authentication** for securing endpoints
- **Unit of Work + Repository Pattern** for data management

---

## Glossary

- **Customer**: A registered user with the "Customer" role, owning an `AppUser` account linked to a `Customer` entity.
- **Cart**: The customer's shopping cart, containing a collection of `CartItem` objects.
- **CartItem**: An item inside the Cart representing a specific product with quantity, price at time of addition, color, and size.
- **Product**: A catalog item with a `StockQuantity` that is decremented upon order completion.
- **Order**: A purchase order automatically created after a successful payment, containing an `OrderItem` for each product.
- **OrderItem**: A line inside an Order representing a product with its quantity and price at time of purchase.
- **Payment**: A payment record in the database, linked to a `StripePaymentIntentId` with a status of `pending`, `succeeded`, or `failed`.
- **PaymentIntent**: A Stripe object representing a payment session, created server-side and returning a `clientSecret` to the frontend.
- **ClientSecret**: A secret key sent by the server to the frontend for use by Stripe.js to confirm the payment.
- **Webhook**: An HTTP request sent by Stripe to the server when events occur, such as payment success or failure.
- **WebhookSecret**: A secret key used to verify the signature of incoming webhook requests from Stripe.
- **JWT_Token**: An authentication token sent by the client in every protected request.
- **OrderStatus**: The order state: `Pending`, `Processing`, `Shipped`, `Delivered`, `Cancelled`, `Returned`.
- **PaymentService**: The service responsible for creating PaymentIntents and handling webhook events.
- **CartService**: The service responsible for managing the Cart and its operations.
- **OrderService**: The service responsible for retrieving and managing Orders.
- **PaymentController**: The controller responsible for receiving payment requests and webhook events.
- **Stripe**: The external payment gateway used in the project.

---

## Requirements

### Requirement 1: Customer Authentication for Checkout Access

**User Story:** As a customer, I want to authenticate before accessing the checkout flow, so that my cart and payment data are securely associated with my account.

#### Acceptance Criteria

1. WHEN a customer sends a request to `POST /api/payment/create-payment-intent` without a valid JWT_Token, THE PaymentController SHALL return HTTP 401 Unauthorized.
2. WHEN a customer sends a request to `GET /api/cart` without a valid JWT_Token, THE CartController SHALL return HTTP 401 Unauthorized.
3. WHEN a valid JWT_Token is provided, THE PaymentController SHALL extract the `customerId` from the `NameIdentifier` claim.
4. IF the `customerId` claim is missing or empty in a valid JWT_Token, THEN THE PaymentController SHALL return HTTP 401 with the message "Customer ID not found".
5. THE JWT_Token SHALL be valid for the duration specified in the `JWT:DurationInDays` configuration (currently 7 days).

---

### Requirement 2: Cart Management Before Checkout

**User Story:** As a customer, I want to manage my cart before checkout, so that I can review and adjust the items I intend to purchase.

#### Acceptance Criteria

1. WHEN a customer sends `GET /api/cart` with a valid JWT_Token, THE CartService SHALL return the customer's current cart with all CartItems including product name, quantity, price, size, and color.
2. WHEN a customer sends `POST /api/cart/items` with a valid `productId` and `quantity`, THE CartService SHALL add the CartItem to the cart with `PriceAtAddition` set to the product's current price at the time of addition.
3. WHEN a customer requests to add a product with `quantity` exceeding the product's `StockQuantity`, THE CartService SHALL return a failure result with the message "Not enough stock available. Only {n} items left."
4. WHEN a customer sends `PUT /api/cart/items/{cartItemId}/increase`, THE CartService SHALL increment the CartItem quantity by 1, provided the resulting quantity does not exceed `StockQuantity`.
5. WHEN a customer sends `PUT /api/cart/items/{cartItemId}/decrease` and the CartItem quantity is greater than 1, THE CartService SHALL decrement the CartItem quantity by 1.
6. WHEN a customer sends `PUT /api/cart/items/{cartItemId}/decrease` and the CartItem quantity equals 1, THE CartService SHALL remove the CartItem from the cart.
7. WHEN a customer sends `DELETE /api/cart/items/{cartItemId}`, THE CartService SHALL remove the specified CartItem from the cart.
8. WHEN a customer sends `DELETE /api/cart/clear`, THE CartService SHALL remove all CartItems from the cart.
9. IF a CartItem does not belong to the authenticated customer's cart, THEN THE CartService SHALL return a failure result with the message "Cart item does not belong to your cart".
10. WHEN a customer has no existing cart, THE CartService SHALL create a new empty cart automatically upon the first cart access.

---

### Requirement 3: Payment Intent Creation

**User Story:** As a customer, I want to initiate a payment session, so that I can securely provide my payment details through Stripe.

#### Acceptance Criteria

1. WHEN a customer sends `POST /api/payment/create-payment-intent` with a valid JWT_Token, THE PaymentService SHALL retrieve the cart associated with the authenticated `customerId`.
2. IF the customer has no cart, THEN THE PaymentService SHALL return a failure result with the message "Cart not found".
3. IF the customer's cart contains no CartItems, THEN THE PaymentService SHALL return a failure result with the message "Cart is empty".
4. WHEN the cart is valid and non-empty, THE PaymentService SHALL calculate `totalAmount` as the sum of `(CartItem.Quantity × CartItem.PriceAtAddition)` for all CartItems.
5. WHEN creating a PaymentIntent, THE PaymentService SHALL send the amount to Stripe in cents (i.e., `totalAmount × 100`) with currency "usd" and payment method type "card".
6. WHEN creating a PaymentIntent, THE PaymentService SHALL include the `customerId` in the Stripe metadata under the key "customerId".
7. WHEN the Stripe PaymentIntent is created successfully, THE PaymentService SHALL persist a Payment record with `Status = "pending"`, `StripePaymentIntentId`, `Amount = totalAmount`, and `CustomerId`.
8. WHEN the PaymentIntent is created successfully, THE PaymentService SHALL return a `PaymentIntentResponseDto` containing `ClientSecret` and `Amount`.
9. IF Stripe returns a `StripeException`, THEN THE PaymentService SHALL return a failure result with the message "Stripe error: {StripeError.Message}".
10. IF an unexpected exception occurs during PaymentIntent creation, THEN THE PaymentService SHALL return a failure result with the message "Error creating payment intent: {exception.Message}".
11. THE `CreatePaymentIntentDto.CartId` field SHALL be accepted in the request body but THE PaymentService SHALL resolve the cart using the `customerId` from the JWT_Token, not the `CartId` from the DTO.

---

### Requirement 4: Stripe Webhook Processing — Payment Succeeded

**User Story:** As the system, I want to automatically create an order when a payment succeeds, so that the customer's purchase is recorded without requiring additional client-side actions.

#### Acceptance Criteria

1. WHEN Stripe sends a `POST /api/payment/webhook` request, THE PaymentController SHALL read the raw request body and verify the `Stripe-Signature` header using the configured `Stripe:WebhookSecret`.
2. IF the `Stripe-Signature` verification fails, THEN THE PaymentController SHALL return HTTP 400 with the message "Webhook signature verification failed: {message}".
3. IF the `Stripe:WebhookSecret` is not configured or is empty, THEN THE PaymentController SHALL return HTTP 500 with the message "Webhook secret not configured".
4. WHEN a `payment_intent.succeeded` event is received, THE PaymentService SHALL look up the Payment record by `StripePaymentIntentId`.
5. IF no Payment record is found for the given `StripePaymentIntentId`, THEN THE PaymentService SHALL return a failure result with the message "Payment record not found".
6. WHEN the Payment record already has `Status = "succeeded"` (idempotency guard), THE PaymentService SHALL return a success result without creating a duplicate Order.
7. WHEN the Payment record has `Status = "pending"`, THE PaymentService SHALL update `Status` to "succeeded" and set `PaymentDate` to the current UTC time.
8. WHEN processing a succeeded payment, THE PaymentService SHALL retrieve the cart associated with the `Payment.CustomerId`.
9. IF the cart is not found or is empty at webhook processing time, THEN THE PaymentService SHALL return a failure result with the message "Cart not found or already cleared".
10. WHEN the cart is valid, THE PaymentService SHALL create an Order with `CustomerId`, `TotalAmount = Payment.Amount`, `Status = OrderStatus.Processing`, and a reference to the Payment record.
11. WHEN creating the Order, THE PaymentService SHALL create an OrderItem for each CartItem with `ProductId`, `ProductName`, `ProductSKU`, `UnitPrice = CartItem.PriceAtAddition`, `Quantity`, `Subtotal = Quantity × UnitPrice`, `SelectedSize`, and `SelectedColor`.
12. WHEN creating the Order, THE PaymentService SHALL decrement each Product's `StockQuantity` by the corresponding CartItem's `Quantity`.
13. WHEN the Order is created successfully, THE PaymentService SHALL clear all CartItems from the customer's cart.
14. THE PaymentController SHALL always return HTTP 200 to Stripe after processing a webhook event, regardless of the internal handler result, to prevent Stripe from retrying the event.

---

### Requirement 5: Stripe Webhook Processing — Payment Failed

**User Story:** As the system, I want to record payment failures, so that the customer's payment status is accurately reflected and the cart remains intact for retry.

#### Acceptance Criteria

1. WHEN a `payment_intent.payment_failed` event is received, THE PaymentService SHALL look up the Payment record by `StripePaymentIntentId`.
2. IF no Payment record is found for the given `StripePaymentIntentId`, THEN THE PaymentService SHALL return a failure result with the message "Payment record not found".
3. WHEN the Payment record is found, THE PaymentService SHALL update `Payment.Status` to "failed".
4. WHEN a payment fails, THE PaymentService SHALL NOT clear the customer's cart, so the customer can retry the payment.
5. WHEN a payment fails, THE PaymentService SHALL NOT decrement any Product's `StockQuantity`.
6. IF an unexpected exception occurs during failure handling, THEN THE PaymentService SHALL return a failure result with the message "Error handling payment failed: {exception.Message}".

---

### Requirement 6: Order Retrieval After Payment

**User Story:** As a customer, I want to view my orders after a successful payment, so that I can track my purchases.

#### Acceptance Criteria

1. WHEN a customer sends `GET /api/orders` with a valid JWT_Token and role "Customer", THE OrderService SHALL return all orders associated with the authenticated `customerId`.
2. WHEN a customer sends `GET /api/orders/{id}` with a valid JWT_Token, THE OrderService SHALL return the order details including all OrderItems if the order belongs to the authenticated customer.
3. IF the requested order does not belong to the authenticated customer and the customer is not an Admin, THEN THE OrderService SHALL return a failure result with the message "Unauthorized access to order".
4. WHEN an Order is returned, THE OrderDto SHALL include `CustomerName`, `OrderDate`, `Status`, `TotalAmount`, `OrderNotes`, and a list of `OrderItemDto`.
5. WHEN an Admin sends `GET /api/orders/admin` with role "Admin", THE OrderService SHALL return a paginated list of all orders with support for `page` and `pageSize` query parameters.
6. WHEN an Admin sends `PUT /api/orders/{id}/status` with role "Admin", THE OrderService SHALL update the order's `Status` to the provided `OrderStatus` value.
7. IF a non-Admin user attempts to update an order status, THEN THE OrderService SHALL return a failure result with the message "Unauthorized. Only administrators can update order status".

---

### Requirement 7: Stock Consistency and Data Integrity

**User Story:** As the system, I want to ensure stock levels are accurate after each order, so that customers cannot purchase more items than are available.

#### Acceptance Criteria

1. WHEN an Order is created via the webhook handler, THE PaymentService SHALL decrement each Product's `StockQuantity` within the same database transaction as the Order creation.
2. WHEN a customer attempts to add a product to the cart with a quantity exceeding the current `StockQuantity`, THE CartService SHALL reject the request before any stock change occurs.
3. WHEN a customer attempts to increase a CartItem quantity beyond the available `StockQuantity`, THE CartService SHALL return a failure result with the message "Cannot increase quantity. Not enough items available in stock."
4. THE PaymentService SHALL persist the Payment record, create the Order, decrement stock, and clear the cart in a single `SaveChangesAsync` call to maintain data consistency.
5. IF the `SaveChangesAsync` call fails during order creation, THEN THE PaymentService SHALL return a failure result and no partial changes SHALL be committed.

---

### Requirement 8: Idempotency and Webhook Security

**User Story:** As the system, I want to handle duplicate webhook events safely, so that a payment success event received more than once does not create duplicate orders.

#### Acceptance Criteria

1. WHEN a `payment_intent.succeeded` webhook event is received for a Payment record that already has `Status = "succeeded"`, THE PaymentService SHALL return a success result with value 0 without creating a new Order.
2. WHEN a `payment_intent.succeeded` webhook event is received for a Payment record that already has `Status = "succeeded"`, THE PaymentService SHALL NOT clear the cart again.
3. THE PaymentController SHALL verify the `Stripe-Signature` header on every incoming webhook request before processing any event data.
4. THE `/api/payment/webhook` endpoint SHALL be accessible without authentication (`[AllowAnonymous]`) since it is called by Stripe's servers, not by end users.
5. THE `/api/payment/create-payment-intent` endpoint SHALL require authentication (`[Authorize]`) and SHALL only be accessible to authenticated customers.

---

### Requirement 9: Logging and Monitoring

**User Story:** As a developer/operator, I want the system to log key payment events, so that I can monitor and debug the payment flow in production.

#### Acceptance Criteria

1. WHEN a webhook event is received, THE PaymentController SHALL log the event type and event ID at the Information level with the format "Stripe webhook received: {EventType} | {EventId}".
2. WHEN the webhook signature verification fails, THE PaymentController SHALL log the failure at the Warning level.
3. WHEN the `HandlePaymentSucceededAsync` handler fails, THE PaymentController SHALL log the error at the Error level including the `paymentIntentId` and the error messages.
4. WHEN the `HandlePaymentFailedAsync` handler fails, THE PaymentController SHALL log the error at the Error level including the `paymentIntentId` and the error messages.
5. WHEN the `Stripe:WebhookSecret` is not configured, THE PaymentController SHALL log the misconfiguration at the Error level.
6. WHILE the application is running, THE PaymentController SHALL log unhandled Stripe event types at the Information level with the format "Unhandled Stripe event type: {EventType}".
