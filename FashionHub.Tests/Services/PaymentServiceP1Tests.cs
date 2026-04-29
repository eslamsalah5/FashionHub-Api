using Application.DTOs.Payment;
using Application.Models;
using Application.Services;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Property-based tests for PaymentService.CreatePaymentIntentAsync — Property P1
/// **Validates: Requirements 3.4, 3.5**
/// </summary>
public class PaymentServiceP1Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Custom Arbitrary: generates a non-empty list of CartItems
    // with positive quantities (1–100) and positive prices (0.01–9999.99)
    // ─────────────────────────────────────────────────────────────────────────

    public class CartItemListArbitrary
    {
        /// <summary>
        /// Returns an Arbitrary for a non-empty list of CartItems with
        /// positive quantities and prices, suitable for P1 testing.
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
    // Property P1 — Payment Amount Consistency
    // **Validates: Requirements 3.4, 3.5**
    //
    // For any non-empty cart:
    //   - PaymentIntentResponseDto.Amount == Σ(Quantity × PriceAtAddition)
    //   - Payment.Amount (persisted record) == Σ(Quantity × PriceAtAddition)
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(CartItemListArbitrary) })]
    public bool P1_PaymentAmountConsistency_DtoAndRecordMatchCartTotal(List<CartItem> cartItems)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string customerId = "customer-p1-test";

        decimal expectedTotal = cartItems.Sum(ci => ci.Quantity * ci.PriceAtAddition);

        var cart = new Cart
        {
            Id = 1,
            CustomerId = customerId,
            CartItems = cartItems
        };

        // Capture the Payment entity passed to AddAsync
        Payment? capturedPayment = null;

        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(customerId))
            .ReturnsAsync(cart);

        var mockPaymentRepo = new Mock<IPaymentRepository>();
        mockPaymentRepo
            .Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p => capturedPayment = p)
            .Returns(Task.CompletedTask);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.Payments).Returns(mockPaymentRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Mock IPaymentGateway — gateway just needs to succeed; Amount in the
        // response DTO comes from totalAmount (calculated from cart), not from
        // the gateway result, so we return Amount = 0 here.
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.GatewayName).Returns("stripe");
        mockGateway
            .Setup(g => g.CreateSessionAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>()))
            .ReturnsAsync(ServiceResult<GatewaySessionResult>.Success(
                new GatewaySessionResult
                {
                    ClientSecret     = "pi_test_secret",
                    GatewayPaymentId = "pi_test_p1",
                    Amount           = 0m   // not used by PaymentService for the DTO
                }));

        var service = new PaymentService(mockUow.Object, new[] { mockGateway.Object });
        var dto = new CreatePaymentIntentDto { CartId = 0, Gateway = "stripe" };

        // ── Act ───────────────────────────────────────────────────────────────
        var result = service.CreatePaymentIntentAsync(dto, customerId).GetAwaiter().GetResult();

        // ── Assert ────────────────────────────────────────────────────────────

        // 1. Service call must succeed
        if (!result.IsSuccess)
            return false;

        // 2. DTO amount must equal the cart total in dollars (Requirement 3.5)
        if (result.Data?.Amount != expectedTotal)
            return false;

        // 3. Persisted Payment.Amount must equal the cart total in dollars (Requirement 3.4)
        if (capturedPayment?.Amount != expectedTotal)
            return false;

        return true;
    }
}
