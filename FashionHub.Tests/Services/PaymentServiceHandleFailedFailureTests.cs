using Application.DTOs.Payment;
using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using Moq;

namespace FashionHub.Tests.Services;

/// <summary>
/// Unit tests for PaymentService.HandlePaymentFailedAsync — failure paths.
/// Validates: Requirements 5.2, 5.6
/// </summary>
public class PaymentServiceHandleFailedFailureTests
{
    private const string PaymentIntentId = "pi_test_handle_failed_failure";

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (Mock<IUnitOfWork> mockUow, Mock<IPaymentRepository> mockPaymentRepo)
        BuildMocks()
    {
        var mockPaymentRepo = new Mock<IPaymentRepository>();
        var mockCartRepo = new Mock<ICartRepository>();
        var mockOrderRepo = new Mock<IOrderRepository>();

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Payments).Returns(mockPaymentRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(r => r.GetByIdAsync(It.IsAny<string>())).ReturnsAsync((AppUser?)null);
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Orders).Returns(mockOrderRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        return (mockUow, mockPaymentRepo);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — Payment record not found → Failure("Payment record not found")
    // Validates: Requirement 5.2
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandlePaymentFailed_PaymentNotFound_ReturnsFailure()
    {
        // Arrange
        var (mockUow, mockPaymentRepo) = BuildMocks();

        mockPaymentRepo
            .Setup(r => r.GetByGatewayPaymentIdAsync(PaymentIntentId))
            .ReturnsAsync((Payment?)null);

        var service = new PaymentService(mockUow.Object, Array.Empty<Application.Services.Interfaces.IPaymentGateway>());

        // Act
        var result = await service.HandlePaymentFailedAsync(new GatewayWebhookEvent { GatewayPaymentId = PaymentIntentId });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("Payment record not found", result.Errors);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — Unexpected exception → Failure("Error handling payment failed: ...")
    // Validates: Requirement 5.6
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task HandlePaymentFailed_UnexpectedException_ReturnsFailure()
    {
        // Arrange
        var (mockUow, mockPaymentRepo) = BuildMocks();

        mockPaymentRepo
            .Setup(r => r.GetByGatewayPaymentIdAsync(PaymentIntentId))
            .ThrowsAsync(new InvalidOperationException("Database connection lost"));

        var service = new PaymentService(mockUow.Object, Array.Empty<Application.Services.Interfaces.IPaymentGateway>());

        // Act
        var result = await service.HandlePaymentFailedAsync(new GatewayWebhookEvent { GatewayPaymentId = PaymentIntentId });

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Single(result.Errors);
        Assert.StartsWith("Error handling payment failed:", result.Errors[0]);
    }
}







