using Application.DTOs.Payment;
using Application.Models;
using Application.Services;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using Moq;

namespace FashionHub.Tests.Services;

/// <summary>
/// Unit tests for PaymentService.CreatePaymentIntentAsync — failure paths.
/// Validates: Requirements 3.2, 3.3, 3.9, 3.10
/// </summary>
public class PaymentServiceFailureTests
{
    private const string CustomerId = "customer-failure-test";

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (Mock<IUnitOfWork> mockUow, Mock<ICartRepository> mockCartRepo, Mock<IPaymentRepository> mockPaymentRepo)
        BuildMocks()
    {
        var mockCartRepo = new Mock<ICartRepository>();
        var mockPaymentRepo = new Mock<IPaymentRepository>();

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.Payments).Returns(mockPaymentRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        return (mockUow, mockCartRepo, mockPaymentRepo);
    }

    /// <summary>
    /// Creates a mock IPaymentGateway with GatewayName = "stripe" and no
    /// CreateSessionAsync setup (not needed for cart-validation failure paths).
    /// </summary>
    private static Mock<IPaymentGateway> BuildStripeGatewayMock()
    {
        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.GatewayName).Returns("stripe");
        return mockGateway;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — Cart not found → Failure("Cart not found")
    // Validates: Requirement 3.2
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePaymentIntent_CartNotFound_ReturnsFailure()
    {
        // Arrange
        var (mockUow, mockCartRepo, _) = BuildMocks();

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync((Cart?)null);

        var mockGateway = BuildStripeGatewayMock();
        var service = new PaymentService(mockUow.Object, new[] { mockGateway.Object });
        var dto = new CreatePaymentIntentDto { CartId = 0, Gateway = "stripe" };

        // Act
        var result = await service.CreatePaymentIntentAsync(dto, CustomerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Cart not found", result.Errors);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — Empty cart → Failure("Cart is empty")
    // Validates: Requirement 3.3
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePaymentIntent_EmptyCart_ReturnsFailure()
    {
        // Arrange
        var (mockUow, mockCartRepo, _) = BuildMocks();

        var emptyCart = new Cart
        {
            Id = 1,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>()   // no items
        };

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync(emptyCart);

        var mockGateway = BuildStripeGatewayMock();
        var service = new PaymentService(mockUow.Object, new[] { mockGateway.Object });
        var dto = new CreatePaymentIntentDto { CartId = 0, Gateway = "stripe" };

        // Act
        var result = await service.CreatePaymentIntentAsync(dto, CustomerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Cart is empty", result.Errors);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — Gateway returns failure → Failure("Stripe error: ...")
    // Validates: Requirement 3.9
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePaymentIntent_StripeException_ReturnsFailure()
    {
        // Arrange
        var (mockUow, mockCartRepo, _) = BuildMocks();

        var cart = new Cart
        {
            Id = 1,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>
            {
                new CartItem
                {
                    Id = 1,
                    ProductId = 10,
                    Quantity = 2,
                    PriceAtAddition = 49.99m,
                    SelectedSize = "M",
                    SelectedColor = "Black",
                    Product = new Domain.Entities.Product
                    {
                        Id = 10,
                        Name = "Test Product",
                        SKU = "SKU-10",
                        StockQuantity = 5
                    }
                }
            }
        };

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync(cart);

        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.GatewayName).Returns("stripe");
        mockGateway
            .Setup(g => g.CreateSessionAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<GatewaySessionResult>.Failure("Stripe error: Your card was declined."));

        var service = new PaymentService(mockUow.Object, new[] { mockGateway.Object });
        var dto = new CreatePaymentIntentDto { CartId = 0, Gateway = "stripe" };

        // Act
        var result = await service.CreatePaymentIntentAsync(dto, CustomerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.StartsWith("Stripe error:", result.Errors[0]);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4 — Unexpected exception → Failure("Error creating payment session: ...")
    // Validates: Requirement 3.10
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePaymentIntent_UnexpectedException_ReturnsFailure()
    {
        // Arrange
        var (mockUow, mockCartRepo, _) = BuildMocks();

        var cart = new Cart
        {
            Id = 1,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>
            {
                new CartItem
                {
                    Id = 2,
                    ProductId = 20,
                    Quantity = 1,
                    PriceAtAddition = 99.00m,
                    SelectedSize = "L",
                    SelectedColor = "White",
                    Product = new Domain.Entities.Product
                    {
                        Id = 20,
                        Name = "Another Product",
                        SKU = "SKU-20",
                        StockQuantity = 3
                    }
                }
            }
        };

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync(cart);

        var mockGateway = new Mock<IPaymentGateway>();
        mockGateway.Setup(g => g.GatewayName).Returns("stripe");
        mockGateway
            .Setup(g => g.CreateSessionAsync(
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<GatewaySessionResult>.Failure("Error creating payment session: Database connection lost."));

        var service = new PaymentService(mockUow.Object, new[] { mockGateway.Object });
        var dto = new CreatePaymentIntentDto { CartId = 0, Gateway = "stripe" };

        // Act
        var result = await service.CreatePaymentIntentAsync(dto, CustomerId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.StartsWith("Error creating payment session:", result.Errors[0]);
    }
}
