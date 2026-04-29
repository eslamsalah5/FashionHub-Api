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
/// Property-based tests for PaymentService.HandlePaymentFailedAsync — Property P6
/// **Validates: Requirements 5.4, 5.5**
/// </summary>
public class PaymentServiceP6Tests
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
        /// unique ProductIds, positive quantities and prices, suitable for P6 testing.
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
    // Property P6 — Cart Preserved After Failure
    // **Validates: Requirements 5.4, 5.5**
    //
    // After HandlePaymentFailedAsync completes, the customer's cart must contain
    // the same CartItem records as before the call:
    //   - ClearCartAsync was never called (Requirement 5.4)
    //   - No CartItem.Quantity was modified (Requirement 5.5)
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(CartItemListArbitrary) })]
    public bool P6_CartPreservedAfterFailure_ClearCartNeverCalledAndQuantitiesUnchanged(List<CartItem> cartItems)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string customerId      = "customer-p6-test";
        const string paymentIntentId = "pi_test_p6";
        const int    cartId          = 99;

        // Snapshot quantities before the call so we can verify they are unchanged
        var quantitiesBefore = cartItems
            .ToDictionary(ci => ci.Id, ci => ci.Quantity);

        var payment = new Payment
        {
            Id               = 1,
            Amount           = cartItems.Sum(ci => ci.Quantity * ci.PriceAtAddition),
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
        // HandlePaymentFailedAsync does NOT access the cart at all, but we set up
        // the mock to detect any unexpected calls to ClearCartAsync.
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(customerId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.ClearCartAsync(It.IsAny<int>()))
            .ReturnsAsync(true);

        // ── Mock: IOrderRepository ────────────────────────────────────────────
        var mockOrderRepo = new Mock<IOrderRepository>();

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

        // ── Act ───────────────────────────────────────────────────────────────
        var result = service.HandlePaymentFailedAsync(paymentIntentId).GetAwaiter().GetResult();

        // ── Assert ────────────────────────────────────────────────────────────

        // 1. Service call must succeed
        if (!result.IsSuccess)
            return false;

        // 2. ClearCartAsync must NEVER have been called — cart must remain intact
        //    (Requirement 5.4: payment failure must NOT clear the cart)
        mockCartRepo.Verify(
            r => r.ClearCartAsync(It.IsAny<int>()),
            Times.Never,
            "ClearCartAsync must never be called when a payment fails");

        // 3. No CartItem.Quantity must have been modified
        //    (Requirement 5.5: payment failure must NOT decrement stock or alter cart items)
        foreach (var cartItem in cartItems)
        {
            int expectedQuantity = quantitiesBefore[cartItem.Id];
            if (cartItem.Quantity != expectedQuantity)
                return false;
        }

        return true;
    }
}
