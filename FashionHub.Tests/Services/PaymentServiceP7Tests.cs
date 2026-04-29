using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Property-based tests for PaymentService.HandlePaymentSucceededAsync — Property P7
/// **Validates: Requirements 8.1, 8.2**
/// </summary>
public class PaymentServiceP7Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Custom Arbitrary: generates a non-empty list of CartItems
    // with positive quantities (1–100) and positive prices (0.01–9999.99)
    // Each product has StockQuantity = quantity + extra to ensure sufficient stock.
    // ─────────────────────────────────────────────────────────────────────────

    public class CartItemListArbitrary
    {
        /// <summary>
        /// Returns an Arbitrary for a non-empty list of CartItems with
        /// unique ProductIds, positive quantities and prices, suitable for P7 testing.
        /// </summary>
        public static Arbitrary<List<CartItem>> CartItems()
        {
            // Generate a price in cents (1–999999) then convert to dollars
            var priceGen = Gen.Choose(1, 999999).Select(cents => cents / 100m);

            // Generate a quantity between 1 and 100
            var quantityGen = Gen.Choose(1, 100);

            // Generate a non-empty list of 1–10 cart items with unique ProductIds
            // by picking a count, then building items with sequential product IDs
            // to guarantee uniqueness (no duplicate ProductId in the same cart).
            var listGen =
                from count in Gen.Choose(1, 10)
                from baseId in Gen.Choose(1, 990)
                from quantities in Gen.ListOf<int>(quantityGen, count)
                from prices in Gen.ListOf<decimal>(priceGen, count)
                from extras in Gen.ListOf<int>(Gen.Choose(1, 500), count)
                let items = Enumerable.Range(0, count).Select(i =>
                {
                    int productId    = baseId + i;          // unique within this list
                    int quantity     = quantities[i];
                    decimal price    = prices[i];
                    int initialStock = quantity + extras[i]; // always > quantity
                    return new CartItem
                    {
                        Id              = productId,
                        ProductId       = productId,
                        Quantity        = quantity,
                        PriceAtAddition = price,
                        SelectedSize    = "M",
                        SelectedColor   = "Blue",
                        Product         = new DomainProduct
                        {
                            Id            = productId,
                            Name          = $"Product {productId}",
                            SKU           = $"SKU-{productId}",
                            StockQuantity = initialStock
                        }
                    };
                }).ToList()
                select items;

            return listGen.ToArbitrary();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property P7 — Idempotency
    // **Validates: Requirements 8.1, 8.2**
    //
    // Calling HandlePaymentSucceededAsync twice with the same paymentIntentId must:
    //   - Result in exactly one Order created (not two)
    //   - Clear the cart only once
    //   - Have the second call return Success(0)
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(CartItemListArbitrary) })]
    public bool P7_Idempotency_SecondCallReturnsSuccessZeroWithNoSideEffects(List<CartItem> cartItems)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string customerId      = "customer-p7-test";
        const string paymentIntentId = "pi_test_p7";
        const int    cartId          = 99;

        decimal paymentAmount = cartItems.Sum(ci => ci.Quantity * ci.PriceAtAddition);

        // The payment object is shared — the service mutates payment.Status directly
        // in memory, so after the first call it will be "succeeded".
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
            Id         = cartId,
            CustomerId = customerId,
            CartItems  = cartItems
        };

        // ── Mock: IPaymentRepository ──────────────────────────────────────────
        // Always returns the same payment object. After the first call the service
        // will have mutated payment.Status to "succeeded" in memory, so the second
        // call will see Status == "succeeded" and hit the idempotency guard.
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
            .Setup(r => r.ClearCartAsync(cartId))
            .ReturnsAsync(true);

        // ── Mock: IOrderRepository ────────────────────────────────────────────
        var mockOrderRepo = new Mock<IOrderRepository>();
        mockOrderRepo
            .Setup(r => r.AddAsync(It.IsAny<Order>()))
            .Returns(Task.CompletedTask);

        // ── Mock: IProductRepository ──────────────────────────────────────────
        var mockProductRepo = new Mock<IProductRepository>();

        // ── Mock: IUnitOfWork ─────────────────────────────────────────────────
        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Payments).Returns(mockPaymentRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.Orders).Returns(mockOrderRepo.Object);
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var service = new PaymentService(mockUow.Object, Array.Empty<Application.Services.Interfaces.IPaymentGateway>());

        // ── Act — First call ──────────────────────────────────────────────────
        var firstResult = service.HandlePaymentSucceededAsync(paymentIntentId).GetAwaiter().GetResult();

        // ── Act — Second call (same paymentIntentId) ──────────────────────────
        // At this point payment.Status == "succeeded" in memory (mutated by first call).
        var secondResult = service.HandlePaymentSucceededAsync(paymentIntentId).GetAwaiter().GetResult();

        // ── Assert ────────────────────────────────────────────────────────────

        // 1. First call must succeed
        if (!firstResult.IsSuccess)
            return false;

        // 2. Second call must return Success(0) — idempotency guard (Requirement 8.1)
        if (!secondResult.IsSuccess)
            return false;

        if (secondResult.Data != 0)
            return false;

        // 3. Orders.AddAsync must have been called exactly once total (Requirement 8.1)
        //    — the second call must NOT create a duplicate order
        mockOrderRepo.Verify(
            r => r.AddAsync(It.IsAny<Order>()),
            Times.Once,
            "Orders.AddAsync must be called exactly once — duplicate order must not be created");

        // 4. ClearCartAsync must have been called exactly once total (Requirement 8.2)
        //    — the second call must NOT clear the cart again
        mockCartRepo.Verify(
            r => r.ClearCartAsync(cartId),
            Times.Once,
            "ClearCartAsync must be called exactly once — cart must not be cleared twice");

        return true;
    }
}
