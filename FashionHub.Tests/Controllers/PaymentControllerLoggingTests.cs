using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using FashionHub.Tests.Helpers;
using Microsoft.Extensions.Logging;
using Moq;

namespace FashionHub.Tests.Controllers;

/// <summary>
/// Unit tests for PaymentController logging behaviour.
/// Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 9.6
/// </summary>
public class PaymentControllerLoggingTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — Webhook receipt logs event type and ID at Information level
    // Validates: Requirement 9.1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_ValidEvent_LogsInformationWithEventTypeAndId()
    {
        const string eventId   = "evt_test_logging_001";
        const string eventType = GatewayEventTypes.PaymentSucceeded;

        var webhookEvent = new GatewayWebhookEvent
        {
            EventType        = eventType,
            GatewayPaymentId = "pi_test_001",
            EventId          = eventId
        };

        var mockGateway = PaymentTestHelpers.BuildMockGateway(webhookEvent: webhookEvent);

        var mockPaymentService = new Mock<IPaymentService>();
        mockPaymentService
            .Setup(s => s.HandlePaymentSucceededAsync(It.IsAny<string>()))
            .ReturnsAsync(ServiceResult<int>.Success(1));

        var (controller, mockLogger) = PaymentTestHelpers.BuildController(
            paymentService: mockPaymentService.Object,
            gateway: mockGateway.Object,
            requestBody: "{}");

        await controller.Webhook(PaymentTestHelpers.GatewayName);

        PaymentTestHelpers.VerifyLog(
            mockLogger,
            LogLevel.Information,
            msg => msg.Contains(eventType) && msg.Contains(eventId),
            Times.Once());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — Invalid signature logs at Warning level
    // Validates: Requirement 9.2
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_InvalidSignature_LogsWarning()
    {
        var mockGateway = PaymentTestHelpers.BuildMockGateway(signatureValid: false);

        var (controller, mockLogger) = PaymentTestHelpers.BuildController(
            gateway: mockGateway.Object,
            requestBody: "{}",
            stripeSignatureHeader: "t=1234,v1=badhex");

        await controller.Webhook(PaymentTestHelpers.GatewayName);

        PaymentTestHelpers.VerifyLog(
            mockLogger,
            LogLevel.Warning,
            msg => msg.Length > 0,
            Times.Once());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — Failed HandlePaymentSucceededAsync logs at Error level with gatewayPaymentId
    // Validates: Requirement 9.3
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_HandlePaymentSucceededFails_LogsErrorWithPaymentId()
    {
        const string paymentId = "pi_test_error_001";

        var webhookEvent = new GatewayWebhookEvent
        {
            EventType        = GatewayEventTypes.PaymentSucceeded,
            GatewayPaymentId = paymentId,
            EventId          = "evt_test_002"
        };

        var mockGateway = PaymentTestHelpers.BuildMockGateway(webhookEvent: webhookEvent);

        var mockPaymentService = new Mock<IPaymentService>();
        mockPaymentService
            .Setup(s => s.HandlePaymentSucceededAsync(paymentId))
            .ReturnsAsync(ServiceResult<int>.Failure("Payment record not found"));

        var (controller, mockLogger) = PaymentTestHelpers.BuildController(
            paymentService: mockPaymentService.Object,
            gateway: mockGateway.Object,
            requestBody: "{}");

        await controller.Webhook(PaymentTestHelpers.GatewayName);

        PaymentTestHelpers.VerifyLog(
            mockLogger,
            LogLevel.Error,
            msg => msg.Contains(paymentId),
            Times.Once());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4 — Unconfigured webhook secret logs at Error level
    // Validates: Requirement 9.5
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_SecretNotConfigured_LogsError()
    {
        var unconfiguredGateway = PaymentTestHelpers.BuildUnconfiguredGateway();

        var (controller, mockLogger) = PaymentTestHelpers.BuildController(
            gateway: unconfiguredGateway.Object,
            requestBody: "{}");

        await controller.Webhook(PaymentTestHelpers.GatewayName);

        PaymentTestHelpers.VerifyLog(
            mockLogger,
            LogLevel.Error,
            msg => msg.Length > 0,
            Times.Once());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5 — Failed HandlePaymentFailedAsync logs at Error level with gatewayPaymentId
    // Validates: Requirement 9.4
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_HandlePaymentFailedFails_LogsErrorWithPaymentId()
    {
        const string paymentId = "pi_test_failed_001";

        var webhookEvent = new GatewayWebhookEvent
        {
            EventType        = GatewayEventTypes.PaymentFailed,
            GatewayPaymentId = paymentId,
            EventId          = "evt_test_003"
        };

        var mockGateway = PaymentTestHelpers.BuildMockGateway(webhookEvent: webhookEvent);

        var mockPaymentService = new Mock<IPaymentService>();
        mockPaymentService
            .Setup(s => s.HandlePaymentFailedAsync(paymentId))
            .ReturnsAsync(ServiceResult<bool>.Failure("Payment record not found"));

        var (controller, mockLogger) = PaymentTestHelpers.BuildController(
            paymentService: mockPaymentService.Object,
            gateway: mockGateway.Object,
            requestBody: "{}");

        await controller.Webhook(PaymentTestHelpers.GatewayName);

        PaymentTestHelpers.VerifyLog(
            mockLogger,
            LogLevel.Error,
            msg => msg.Contains(paymentId),
            Times.Once());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6 — Unhandled event type logs at Information level
    // Validates: Requirement 9.6
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_UnhandledEventType_LogsInformationWithEventType()
    {
        const string unhandledType = "customer.subscription.created";

        var webhookEvent = new GatewayWebhookEvent
        {
            EventType        = unhandledType,
            GatewayPaymentId = string.Empty,
            EventId          = "evt_test_004"
        };

        var mockGateway = PaymentTestHelpers.BuildMockGateway(webhookEvent: webhookEvent);

        var (controller, mockLogger) = PaymentTestHelpers.BuildController(
            gateway: mockGateway.Object,
            requestBody: "{}");

        await controller.Webhook(PaymentTestHelpers.GatewayName);

        PaymentTestHelpers.VerifyLog(
            mockLogger,
            LogLevel.Information,
            msg => msg.Contains("Unhandled") && msg.Contains(unhandledType),
            Times.Once());
    }
}
