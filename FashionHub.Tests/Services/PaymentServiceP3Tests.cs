using Application.DTOs.Payment;
using Application.Services;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Property-based tests for PaymentService.HandlePaymentSucceededAsync — Property P3
/// **Validates: Requirements 4.10, 4.11**
/// </summary>
public class PaymentServiceP3Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Custom Arbitrary: generates a non-empty list of CartItems
    // with positive quantities (1–100) and positive prices (0.01–9999.99)
    // ─────────────────────────────────────────────────────────────────────────

    public class CartItemListArbitrary
    {
        /// <summary>
        /// Returns an Arbitrary for a non-empty list of CartItems with
        /// positive quantities and prices, suitable for P3 testing.
        /// </summary>
        public static Arbitrary<List<CartItem>> CartItems()
        {
            // Generate a price in cents (1–999999) then convert to dollars
            var priceGen = Gen.Choose(1, 999999).Select(cents => cents / 100m);

            // Generate a quantity between 1 and 100
            var quantityGen = Gen.Choose(1, 100);

            // Generate a single CartItem using LINQ query syntax
            var cartItemGen =
                from productId in Gen.Choose(1, 1000)
                from quantity in quantityGen
                from price in priceGen
                select new CartItem
                {
                    Id = productId,
                    ProductId = productId,
                    Quantity = quantity,
                    PriceAtAddition = price,
                    SelectedSize = "M",
                    SelectedColor = "Blue",
                    Product = new DomainProduct
                    {
                        Id = productId,
                        Name = $"Product {productId}",
                        SKU = $"SKU-{productId}",
                        StockQuantity = quantity + 10
                    }
                };

            // Generate a non-empty list of 1–10 cart items
            var listGen =
                from count in Gen.Choose(1, 10)
                from items in Gen.ListOf<CartItem>(cartItemGen, count)
                select items;

            return listGen.ToArbitrary();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property P3 — Order Creation on Webhook Success
    // **Validates: Requirements 4.10, 4.11**
    //
    // After HandlePaymentSucceededAsync succeeds, assert:
    //   - Exactly one Order was created (Orders.AddAsync called exactly once)
    //   - The Order.Status == OrderStatus.Processing
    //   - The Order.TotalAmount == Payment.Amount
    //   - One OrderItem per CartItem with matching ProductId, Quantity,
    //     UnitPrice (== PriceAtAddition), and Subtotal (== Quantity × UnitPrice)
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(CartItemListArbitrary) })]
    public bool P3_OrderCreationOnWebhookSuccess_OrderMatchesPaymentAndCartItems(List<CartItem> cartItems)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string customerId       = "customer-p3-test";
        const string paymentIntentId  = "pi_test_p3";

        // Payment.Amount = Σ(Quantity × PriceAtAddition) — matches what CreatePaymentIntentAsync persists
        decimal paymentAmount = cartItems.Sum(ci => ci.Quantity * ci.PriceAtAddition);

        var payment = new Payment
        {
            Id               = 1,
            Amount           = paymentAmount,
            GatewayPaymentId = paymentIntentId,
            Status           = "pending",
            CustomerId       = customerId
        };

        var cart = new Cart
        {
            Id         = 1,
            CustomerId = customerId,
            CartItems  = cartItems
        };

        // Capture the Order entity passed to Orders.AddAsync
        int   orderAddAsyncCallCount = 0;
        Order? capturedOrder         = null;

        // ── Mock: IPaymentRepository ──────────────────────────────────────────
        var mockPaymentRepo = new Mock<IPaymentRepository>();
        mockPaymentRepo
            .Setup(r => r.GetByGatewayPaymentIdAsync(paymentIntentId))
            .ReturnsAsync(payment);

        // ── Mock: ICartRepository ─────────────────────────────────────────────
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(customerId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.ClearCartAsync(cart.Id))
            .ReturnsAsync(true);

        // ── Mock: IOrderRepository ────────────────────────────────────────────
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo
            .Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Callback<Order>(o =>
            {
                orderAddAsyncCallCount++;
                capturedOrder = o;
            })
            .Returns(Task.CompletedTask);

        // ── Mock: IProductRepository ──────────────────────────────────────────
        // The service decrements StockQuantity directly on the Product navigation
        // property (cartItem.Product.StockQuantity -= cartItem.Quantity), so no
        // repository method call is needed — but IUnitOfWork.Products must be set up
        // to avoid NullReferenceException if the mock is accessed.
        var mockProductRepo = new Mock<IProductRepository>();

        // ── Mock: IUnitOfWork ─────────────────────────────────────────────────
        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Payments).Returns(mockPaymentRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Orders).Returns(mockOrderRepo.Object);
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var service = new PaymentService(mockUow.Object, Array.Empty<Application.Services.Interfaces.IPaymentGateway>());

        // ── Act ───────────────────────────────────────────────────────────────
        var result = service.HandlePaymentSucceededAsync(new GatewayWebhookEvent { GatewayPaymentId = paymentIntentId }).GetAwaiter().GetResult();

        // ── Assert ────────────────────────────────────────────────────────────

        // 1. Service call must succeed
        if (!result.IsSuccess)
            return false;

        // 2. Orders.AddAsync must have been called exactly once (Requirement 4.10)
        if (orderAddAsyncCallCount != 1)
            return false;

        // 3. The captured Order must not be null
        if (capturedOrder is null)
            return false;

        // 4. Order.Status must be OrderStatus.Processing (Requirement 4.10)
        if (capturedOrder.Status != OrderStatus.Processing)
            return false;

        // 5. Order.TotalAmount must equal Payment.Amount (Requirement 4.10)
        if (capturedOrder.TotalAmount != paymentAmount)
            return false;

        // 6. One OrderItem per CartItem (Requirement 4.11)
        if (capturedOrder.OrderItems.Count != cartItems.Count)
            return false;

        // 7. Each OrderItem must match its corresponding CartItem (Requirement 4.11)
        //    Sort both by ProductId to allow stable comparison across arbitrary inputs.
        var orderedItems    = capturedOrder.OrderItems.OrderBy(oi => oi.ProductId).ToList();
        var orderedCartItems = cartItems.OrderBy(ci => ci.ProductId).ToList();

        for (int i = 0; i < orderedCartItems.Count; i++)
        {
            var cartItem  = orderedCartItems[i];
            var orderItem = orderedItems[i];

            // ProductId must match (Requirement 4.11)
            if (orderItem.ProductId != cartItem.ProductId)
                return false;

            // Quantity must match (Requirement 4.11)
            if (orderItem.Quantity != cartItem.Quantity)
                return false;

            // UnitPrice must equal CartItem.PriceAtAddition (Requirement 4.11)
            if (orderItem.UnitPrice != cartItem.PriceAtAddition)
                return false;

            // Subtotal must equal Quantity × UnitPrice (Requirement 4.11)
            decimal expectedSubtotal = cartItem.Quantity * cartItem.PriceAtAddition;
            if (orderItem.Subtotal != expectedSubtotal)
                return false;
        }

        return true;
    }
}







