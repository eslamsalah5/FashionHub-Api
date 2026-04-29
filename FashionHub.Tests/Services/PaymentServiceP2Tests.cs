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
/// Property-based tests for PaymentService.CreatePaymentIntentAsync — Property P2
/// **Validates: Requirements 3.7**
/// </summary>
public class PaymentServiceP2Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Custom Arbitrary: generates a non-empty list of CartItems
    // with positive quantities (1–100) and positive prices (0.01–9999.99)
    // ─────────────────────────────────────────────────────────────────────────

    public class CartItemListArbitrary
    {
        /// <summary>
        /// Returns an Arbitrary for a non-empty list of CartItems with
        /// positive quantities and prices, suitable for P2 testing.
        /// </summary>
        public static Arbitrary<List<CartItem>> CartItems()
        {
            // Generate a price in cents (1–999999) then convert to dollars
            var priceGen = Gen.Choose(1, 999999).Select(cents => cents / 100m);

            // Generate a quantity between 1 and 100
            var quantityGen = Gen.Choose(1, 100);

            // Generate a single CartItem
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
    // Custom Arbitrary: generates a non-empty, non-whitespace customerId string
    // ─────────────────────────────────────────────────────────────────────────

    public class NonEmptyStringArbitrary
    {
        /// <summary>
        /// Returns an Arbitrary for non-empty, non-whitespace strings
        /// suitable for use as customer IDs.
        /// </summary>
        public static Arbitrary<string> NonEmptyString()
        {
            // Generate strings of length 1–50 using alphanumeric characters
            char[] alphabet =
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_"
                    .ToCharArray();

            var charGen = Gen.Elements<char>(alphabet);

            var stringGen =
                from length in Gen.Choose(1, 50)
                from chars in Gen.ListOf<char>(charGen, length)
                select new string(chars.ToArray());

            return stringGen.ToArbitrary();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property P2 — Payment Record Persistence
    // **Validates: Requirements 3.7**
    //
    // After a successful CreatePaymentIntentAsync:
    //   - Exactly one Payment record was persisted (AddAsync called exactly once)
    //   - The persisted Payment.Status == "pending"
    //   - The persisted Payment.GatewayPaymentId matches the gateway payment ID returned
    //   - The persisted Payment.CustomerId matches the customerId passed to the method
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(CartItemListArbitrary), typeof(NonEmptyStringArbitrary) })]
    public bool P2_PaymentRecordPersistence_ExactlyOneRecordWithCorrectFields(
        List<CartItem> cartItems,
        string customerId)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string fakePaymentIntentId = "pi_test_p2_fake";
        const string fakeClientSecret    = "pi_test_p2_fake_secret";

        var cart = new Cart
        {
            Id = 1,
            CustomerId = customerId,
            CartItems = cartItems
        };

        // Track how many times AddAsync was called and capture the Payment entity
        int addAsyncCallCount = 0;
        Payment? capturedPayment = null;

        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(customerId))
            .ReturnsAsync(cart);

        var mockPaymentRepo = new Mock<IPaymentRepository>();
        mockPaymentRepo
            .Setup(r => r.AddAsync(It.IsAny<Payment>()))
            .Callback<Payment>(p =>
            {
                addAsyncCallCount++;
                capturedPayment = p;
            })
            .Returns(Task.CompletedTask);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.Payments).Returns(mockPaymentRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Mock IPaymentGateway — return a fake session result with a known payment ID
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.GatewayName).Returns("stripe");
        mockGateway
            .Setup(g => g.CreateSessionAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<GatewaySessionResult>.Success(new GatewaySessionResult
            {
                ClientSecret     = fakeClientSecret,
                GatewayPaymentId = fakePaymentIntentId,
                Amount           = 0m   // amount not relevant for this property
            }));

        var service = new PaymentService(mockUow.Object, new[] { mockGateway.Object });
        var dto = new CreatePaymentIntentDto { CartId = 0, Gateway = "stripe" };

        // ── Act ───────────────────────────────────────────────────────────────
        var result = service.CreatePaymentIntentAsync(dto, customerId).GetAwaiter().GetResult();

        // ── Assert ────────────────────────────────────────────────────────────

        // 1. Service call must succeed
        if (!result.IsSuccess)
            return false;

        // 2. AddAsync must have been called exactly once (Requirement 3.7)
        if (addAsyncCallCount != 1)
            return false;

        // 3. The captured Payment must not be null
        if (capturedPayment is null)
            return false;

        // 4. Persisted Payment.Status must be "pending" (Requirement 3.7)
        if (capturedPayment.Status != "pending")
            return false;

        // 5. Persisted Payment.GatewayPaymentId must match the gateway payment ID (Requirement 3.7)
        if (capturedPayment.GatewayPaymentId != fakePaymentIntentId)
            return false;

        // 6. Persisted Payment.CustomerId must match the customerId passed in (Requirement 3.7)
        if (capturedPayment.CustomerId != customerId)
            return false;

        return true;
    }
}
