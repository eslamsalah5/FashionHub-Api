using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using FashionHub.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Presentation.Controllers;
using System.Security.Claims;
using System.Text;

namespace FashionHub.Tests.Controllers;

/// <summary>
/// Unit tests for PaymentController auth and webhook guards.
/// Validates: Requirements 1.1, 1.4, 4.2, 4.3
/// </summary>
public class PaymentControllerTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static PaymentController CreateControllerWithUser(
        string? nameIdentifierClaim,
        IPaymentService? paymentService = null)
    {
        var (controller, _) = PaymentTestHelpers.BuildController(paymentService: paymentService);

        var claims = nameIdentifierClaim is not null
            ? new[] { new Claim(ClaimTypes.NameIdentifier, nameIdentifierClaim) }
            : Array.Empty<Claim>();

        controller.ControllerContext.HttpContext.User =
            new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        return controller;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — Missing customerId claim → 401 "Customer ID not found"
    // Validates: Requirement 1.4
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CreatePaymentIntent_MissingCustomerIdClaim_Returns401WithMessage()
    {
        var controller = CreateControllerWithUser(nameIdentifierClaim: null);
        var dto = new CreatePaymentIntentDto { CartId = 1 };

        var result = await controller.CreatePaymentIntent(dto);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorizedResult.StatusCode);
        Assert.Equal("Customer ID not found", unauthorizedResult.Value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — Unknown gateway → 400
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_UnknownGateway_Returns400()
    {
        // No gateways registered → any gateway name is unknown
        var (controller, _) = PaymentTestHelpers.BuildController(gateway: null, requestBody: "{}");

        var result = await controller.Webhook("unknown-gateway");

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — Webhook secret not configured → 500
    // Validates: Requirement 4.3
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_SecretNotConfigured_Returns500()
    {
        var unconfiguredGateway = PaymentTestHelpers.BuildUnconfiguredGateway();
        var (controller, _) = PaymentTestHelpers.BuildController(
            gateway: unconfiguredGateway.Object,
            requestBody: "{}");

        var result = await controller.Webhook(PaymentTestHelpers.GatewayName);

        var statusCodeResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusCodeResult.StatusCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4 — Invalid signature → 400
    // Validates: Requirement 4.2
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Webhook_InvalidSignature_Returns400()
    {
        var invalidSigGateway = PaymentTestHelpers.BuildMockGateway(signatureValid: false);
        var (controller, _) = PaymentTestHelpers.BuildController(
            gateway: invalidSigGateway.Object,
            requestBody: "{}",
            stripeSignatureHeader: "t=1234,v1=badhex");

        var result = await controller.Webhook(PaymentTestHelpers.GatewayName);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(400, badRequest.StatusCode);
    }
}
