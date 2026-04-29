using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using Moq;

namespace FashionHub.Tests.Services;

/// <summary>
/// Unit tests for PaymentService.HandlePaymentSucceededAsync — failure paths.
/// Validates: Requirements 4.5, 4.9
/// </summary>
public class PaymentServiceHandleSucceededFailureTests
{
    private const string PaymentIntentId = "pi_test_handle_succeeded_failure";
    private const string CustomerId = "customer-handle-succeeded-failure";

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (Mock<IUnitOfWork> mockUow, Mock<ICartRepository> mockCartRepo, Mock<IPaymentRepository> mockPaymentRepo)
        BuildMocks()
    {
        var mockCartRepo = new Mock<ICartRepository>();
        var mockPaymentRepo = new Mock<IPaymentRepository>();
        var mockOrderRepo = new Mock<IOrderRepository>();

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.Payments).Returns(mockPaymentRepo.Object);
        mockUow.Setup(u => u.Orders).Returns(mockOrderRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        return (mockUow, mockCartRepo, mockPaymentRepo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — Payment record not found → Failure("Payment record not found")
    // Validates: Requirement 4.5
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandlePaymentSucceeded_PaymentNotFound_ReturnsFailure()
    {
        // Arrange
        var (mockUow, _, mockPaymentRepo) = BuildMocks();

        mockPaymentRepo
            .Setup(r => r.GetByGatewayPaymentIdAsync(PaymentIntentId))
            .ReturnsAsync((Payment?)null);

        var service = new PaymentService(mockUow.Object, Array.Empty<Application.Services.Interfaces.IPaymentGateway>());

        // Act
        var result = await service.HandlePaymentSucceededAsync(PaymentIntentId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Payment record not found", result.Errors);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — Cart not found or empty at webhook time → Failure("Cart not found or already cleared")
    // Validates: Requirement 4.9
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandlePaymentSucceeded_CartNotFoundOrEmpty_ReturnsFailure()
    {
        // Arrange
        var (mockUow, mockCartRepo, mockPaymentRepo) = BuildMocks();

        var pendingPayment = new Payment
        {
            Id               = 1,
            GatewayPaymentId = PaymentIntentId,
            Status           = "pending",
            Amount           = 99.99m,
            CustomerId       = CustomerId
        };

        mockPaymentRepo
            .Setup(r => r.GetByGatewayPaymentIdAsync(PaymentIntentId))
            .ReturnsAsync(pendingPayment);

        // Cart is null — simulates cart not found or already cleared
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync((Cart?)null);

        var service = new PaymentService(mockUow.Object, Array.Empty<Application.Services.Interfaces.IPaymentGateway>());

        // Act
        var result = await service.HandlePaymentSucceededAsync(PaymentIntentId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Cart not found or already cleared", result.Errors);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — Cart found but empty → Failure("Cart not found or already cleared")
    // Validates: Requirement 4.9 (empty cart variant)
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandlePaymentSucceeded_CartEmpty_ReturnsFailure()
    {
        // Arrange
        var (mockUow, mockCartRepo, mockPaymentRepo) = BuildMocks();

        var pendingPayment = new Payment
        {
            Id               = 2,
            GatewayPaymentId = PaymentIntentId,
            Status           = "pending",
            Amount           = 49.99m,
            CustomerId       = CustomerId
        };

        mockPaymentRepo
            .Setup(r => r.GetByGatewayPaymentIdAsync(PaymentIntentId))
            .ReturnsAsync(pendingPayment);

        // Cart exists but has no items — simulates already-cleared cart
        var emptyCart = new Cart
        {
            Id = 10,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>()
        };

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync(emptyCart);

        var service = new PaymentService(mockUow.Object, Array.Empty<Application.Services.Interfaces.IPaymentGateway>());

        // Act
        var result = await service.HandlePaymentSucceededAsync(PaymentIntentId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Cart not found or already cleared", result.Errors);
    }
}
