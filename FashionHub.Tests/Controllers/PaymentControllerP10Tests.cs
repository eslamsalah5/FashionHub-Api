using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using FashionHub.Tests.Helpers;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace FashionHub.Tests.Controllers;

/// <summary>
/// Property P10 — Webhook Authorization
/// For any invalid/missing signature, the endpoint must return HTTP 400
/// and never call HandlePaymentSucceededAsync or HandlePaymentFailedAsync.
/// Validates: Requirements 4.1, 4.2, 8.3
/// </summary>
public class PaymentControllerP10Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Arbitrary: generates invalid signature strings
    // ─────────────────────────────────────────────────────────────────────────

    public class InvalidSignatureArbitrary
    {
        public static Arbitrary<string> InvalidSignatures()
        {
            var fixedBad = new[]
            {
                "not-a-signature",
                "t=0,v1=badhex",
                "t=9999999999,v1=0000000000000000000000000000000000000000000000000000000000000000",
                "v1=abc123",
                "t=,v1=",
                "Bearer token123",
                "   ",
                "",
                "t=1234567890",
                "v1=abc",
                "t=abc,v1=xyz",
            };

            var randomGen =
                from chars in Gen.ListOf(Gen.Elements<char>(
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789=,._- "))
                select new string(chars.ToArray());

            return Gen.OneOf(Gen.Elements(fixedBad), randomGen).ToArbitrary();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property P10 — invalid signature → HTTP 400, no service calls
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(InvalidSignatureArbitrary) })]
    public bool P10_InvalidSignature_Returns400AndNoDatabaseWrites(string invalidSignature)
    {
        // Gateway always rejects the signature
        var mockGateway = PaymentTestHelpers.BuildMockGateway(signatureValid: false);
        var mockPaymentService = new Mock<IPaymentService>();

        var (controller, _) = PaymentTestHelpers.BuildController(
            paymentService: mockPaymentService.Object,
            gateway: mockGateway.Object,
            requestBody: "{}",
            stripeSignatureHeader: invalidSignature);

        var result = controller.Webhook(PaymentTestHelpers.GatewayName).GetAwaiter().GetResult();

        if (result is not BadRequestObjectResult { StatusCode: 400 })
            return false;

        mockPaymentService.Verify(s => s.HandlePaymentSucceededAsync(It.IsAny<GatewayWebhookEvent>()), Times.Never());
        mockPaymentService.Verify(s => s.HandlePaymentFailedAsync(It.IsAny<GatewayWebhookEvent>()), Times.Never());

        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Missing signature header → HTTP 400, no service calls
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task P10_MissingSignatureHeader_Returns400AndNoDatabaseWrites()
    {
        var mockGateway = PaymentTestHelpers.BuildMockGateway(signatureValid: false);
        var mockPaymentService = new Mock<IPaymentService>();

        var (controller, _) = PaymentTestHelpers.BuildController(
            paymentService: mockPaymentService.Object,
            gateway: mockGateway.Object,
            requestBody: "{}",
            stripeSignatureHeader: null);

        var result = await controller.Webhook(PaymentTestHelpers.GatewayName);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);

        mockPaymentService.Verify(s => s.HandlePaymentSucceededAsync(It.IsAny<GatewayWebhookEvent>()), Times.Never());
        mockPaymentService.Verify(s => s.HandlePaymentFailedAsync(It.IsAny<GatewayWebhookEvent>()), Times.Never());
    }
}

