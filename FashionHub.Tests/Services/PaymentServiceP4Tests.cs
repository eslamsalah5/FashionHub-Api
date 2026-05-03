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
/// Property-based tests for PaymentService.HandlePaymentSucceededAsync — Property P4
/// **Validates: Requirements 4.12, 7.1**
/// </summary>
public class PaymentServiceP4Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Custom Arbitrary: generates a non-empty list of CartItems
    // with positive quantities (1–100) and positive prices (0.01–9999.99)
    // Each product has StockQuantity = quantity + 10 to ensure sufficient stock.
    // ─────────────────────────────────────────────────────────────────────────

    public class CartItemListArbitrary
    {
        /// <summary>
        /// Returns an Arbitrary for a non-empty list of CartItems with
        /// unique ProductIds, positive quantities and prices, suitable for P4 testing.
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
                from stocks in Gen.ListOf<int>(Gen.Choose(1, 500), count)
                let items = Enumerable.Range(0, count).Select(i =>
                {
                    int productId    = baseId + i;          // unique within this list
                    int quantity     = quantities[i];
                    decimal price    = prices[i];
                    int initialStock = quantity + stocks[i]; // always > quantity
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
    // Property P4 — Stock Decrement Correctness
    // **Validates: Requirements 4.12, 7.1**
    //
    // After HandlePaymentSucceededAsync succeeds, for each product:
    //   StockQuantity_after == StockQuantity_before - OrderItem.Quantity
    //
    // The service decrements stock directly on the Product navigation property:
    //   cartItem.Product.StockQuantity -= cartItem.Quantity
    // So we can verify the in-memory objects after the call.
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(CartItemListArbitrary) })]
    public bool P4_StockDecrementCorrectness_EachProductStockReducedByOrderedQuantity(List<CartItem> cartItems)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string customerId      = "customer-p4-test";
        const string paymentIntentId = "pi_test_p4";

        // Record StockQuantity_before for each product (keyed by ProductId)
        var stockBefore = cartItems.ToDictionary(
            ci => ci.ProductId,
            ci => ci.Product.StockQuantity);

        // Payment.Amount = Σ(Quantity × PriceAtAddition)
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
            .Returns(Task.CompletedTask);

        // ── Mock: IProductRepository ──────────────────────────────────────────
        // Stock is decremented directly on the Product navigation property,
        // so no repository method is called for stock updates.
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

        // 2. For each cart item, verify StockQuantity_after == StockQuantity_before - Quantity
        //    (Requirement 4.12, 7.1)
        foreach (var cartItem in cartItems)
        {
            int before   = stockBefore[cartItem.ProductId];
            int after    = cartItem.Product.StockQuantity;
            int expected = before - cartItem.Quantity;

            if (after != expected)
                return false;
        }

        return true;
    }
}







