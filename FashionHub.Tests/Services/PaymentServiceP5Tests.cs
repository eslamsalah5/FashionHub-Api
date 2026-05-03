using Application.DTOs.Payment;
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
/// Property-based tests for PaymentService.HandlePaymentSucceededAsync — Property P5
/// **Validates: Requirements 4.13**
/// </summary>
public class PaymentServiceP5Tests
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
        /// unique ProductIds, positive quantities and prices, suitable for P5 testing.
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
    // Property P5 — Cart Cleared After Success
    // **Validates: Requirements 4.13**
    //
    // After HandlePaymentSucceededAsync succeeds, the customer's cart must
    // contain zero CartItem records — i.e., ClearCartAsync was called exactly
    // once with the correct cart ID.
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(CartItemListArbitrary) })]
    public bool P5_CartClearedAfterSuccess_ClearCartCalledExactlyOnceWithCorrectCartId(List<CartItem> cartItems)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string customerId      = "customer-p5-test";
        const string paymentIntentId = "pi_test_p5";
        const int    cartId          = 42;

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
            Id         = cartId,
            CustomerId = customerId,
            CartItems  = cartItems
        };

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

        // 2. ClearCartAsync must have been called exactly once with the correct cart ID
        //    (Requirement 4.13)
        mockCartRepo.Verify(
            r => r.ClearCartAsync(cartId),
            Times.Once,
            $"Expected ClearCartAsync to be called exactly once with cartId={cartId}");

        // 3. ClearCartAsync must NOT have been called with any other cart ID
        mockCartRepo.Verify(
            r => r.ClearCartAsync(It.Is<int>(id => id != cartId)),
            Times.Never,
            "ClearCartAsync must not be called with a different cart ID");

        return true;
    }
}







