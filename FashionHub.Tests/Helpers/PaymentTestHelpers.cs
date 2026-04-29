using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Presentation.Controllers;
using System.Security.Cryptography;
using System.Text;

namespace FashionHub.Tests.Helpers;

/// <summary>
/// Shared helpers for PaymentController and PaymentService tests
/// after the multi-gateway refactoring.
/// </summary>
public static class PaymentTestHelpers
{
    public const string WebhookSecret = "whsec_testsecret1234567890abcdef1234567890";
    public const string GatewayName   = "stripe";

    // ─────────────────────────────────────────────────────────────────────────
    // Build a mock IPaymentGateway that behaves like Stripe
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a mock gateway that succeeds on ParseWebhookAsync with the given event.
    /// </summary>
    public static Mock<IPaymentGateway> BuildMockGateway(
        GatewayWebhookEvent? webhookEvent = null,
        bool signatureValid = true)
    {
        var mock = new Mock<IPaymentGateway>();
        mock.Setup(g => g.GatewayName).Returns(GatewayName);

        if (signatureValid && webhookEvent != null)
        {
            mock.Setup(g => g.ParseWebhookAsync(
                    It.IsAny<string>(),
                    It.IsAny<IDictionary<string, string>>()))
                .ReturnsAsync(ServiceResult<GatewayWebhookEvent>.Success(webhookEvent));
        }
        else if (!signatureValid)
        {
            mock.Setup(g => g.ParseWebhookAsync(
                    It.IsAny<string>(),
                    It.IsAny<IDictionary<string, string>>()))
                .ReturnsAsync(ServiceResult<GatewayWebhookEvent>.Failure(
                    "Webhook signature verification failed: invalid signature."));
        }

        return mock;
    }

    /// <summary>
    /// Returns a mock gateway that returns "Webhook secret not configured".
    /// </summary>
    public static Mock<IPaymentGateway> BuildUnconfiguredGateway()
    {
        var mock = new Mock<IPaymentGateway>();
        mock.Setup(g => g.GatewayName).Returns(GatewayName);
        mock.Setup(g => g.ParseWebhookAsync(
                It.IsAny<string>(),
                It.IsAny<IDictionary<string, string>>()))
            .ReturnsAsync(ServiceResult<GatewayWebhookEvent>.Failure(
                "Webhook secret not configured"));
        return mock;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Build a PaymentController with a mock gateway list
    // ─────────────────────────────────────────────────────────────────────────

    public static (PaymentController controller, Mock<ILogger<PaymentController>> mockLogger)
        BuildController(
            IPaymentService? paymentService = null,
            IPaymentGateway? gateway = null,
            string? requestBody = null,
            string? stripeSignatureHeader = null)
    {
        var mockPaymentService = paymentService ?? new Mock<IPaymentService>().Object;
        var gateways = gateway != null
            ? new[] { gateway }
            : Array.Empty<IPaymentGateway>();

        var mockLogger = new Mock<ILogger<PaymentController>>();
        var controller = new PaymentController(mockPaymentService, gateways, mockLogger.Object);

        var httpContext = new DefaultHttpContext();

        if (requestBody is not null)
        {
            var bodyBytes = Encoding.UTF8.GetBytes(requestBody);
            httpContext.Request.Body = new MemoryStream(bodyBytes);
            httpContext.Request.ContentLength = bodyBytes.Length;
        }

        if (stripeSignatureHeader is not null)
            httpContext.Request.Headers["Stripe-Signature"] = stripeSignatureHeader;

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return (controller, mockLogger);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Compute a real Stripe HMAC-SHA256 signature (for tests that need it)
    // ─────────────────────────────────────────────────────────────────────────

    public static string ComputeStripeSignature(string payload, string secret, long timestamp)
    {
        var signedPayload = $"{timestamp}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        return $"t={timestamp},v1={BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant()}";
    }

    // ─────────────────────────────────────────────────────────────────────────
    // ILogger verification helper
    // ─────────────────────────────────────────────────────────────────────────

    public static void VerifyLog<T>(
        Mock<ILogger<T>> mockLogger,
        LogLevel level,
        Func<string, bool> messagePredicate,
        Times times)
    {
        mockLogger.Verify(
            x => x.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => messagePredicate(state.ToString() ?? string.Empty)),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
