using Application.DTOs.Payment;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using System.Security.Claims;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IConfiguration _configuration;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            IConfiguration configuration,
            ILogger<PaymentController> logger)
        {
            _paymentService    = paymentService;
            _configuration     = configuration;
            _logger            = logger;
        }

        // ─────────────────────────────────────────────────────────────
        // POST api/payment/create-payment-intent
        // Authenticated customer calls this to start a payment session.
        // Returns clientSecret that Stripe.js uses on the frontend.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("create-payment-intent")]
        [Authorize]
        public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentDto dto)
        {
            var customerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(customerId))
                return Unauthorized("Customer ID not found");

            var result = await _paymentService.CreatePaymentIntentAsync(dto, customerId);

            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        // ─────────────────────────────────────────────────────────────
        // POST api/payment/webhook
        // Stripe calls this endpoint — NOT the client.
        // Must be [AllowAnonymous] and must read the raw body for signature verification.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> StripeWebhook()
        {
            var webhookSecret = _configuration["Stripe:WebhookSecret"];

            if (string.IsNullOrEmpty(webhookSecret) || webhookSecret.StartsWith("whsec_your"))
            {
                _logger.LogError("Stripe WebhookSecret is not configured.");
                return StatusCode(500, "Webhook secret not configured");
            }

            // Read raw body — required for Stripe signature verification
            string json;
            using (var reader = new StreamReader(HttpContext.Request.Body))
            {
                json = await reader.ReadToEndAsync();
            }

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    Request.Headers["Stripe-Signature"],
                    webhookSecret
                );
            }
            catch (StripeException ex)
            {
                _logger.LogWarning("Stripe webhook signature verification failed: {Message}", ex.Message);
                return BadRequest($"Webhook signature verification failed: {ex.Message}");
            }

            _logger.LogInformation("Stripe webhook received: {EventType} | {EventId}", stripeEvent.Type, stripeEvent.Id);

            // Stripe event types are string constants like "payment_intent.succeeded"
            switch (stripeEvent.Type)
            {
                case "payment_intent.succeeded":
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    if (paymentIntent == null) break;

                    var result = await _paymentService.HandlePaymentSucceededAsync(paymentIntent.Id);
                    if (!result.IsSuccess)
                        _logger.LogError("HandlePaymentSucceeded failed for {Id}: {Errors}", paymentIntent.Id, string.Join(", ", result.Errors));

                    break;
                }

                case "payment_intent.payment_failed":
                {
                    var paymentIntent = stripeEvent.Data.Object as PaymentIntent;
                    if (paymentIntent == null) break;

                    var result = await _paymentService.HandlePaymentFailedAsync(paymentIntent.Id);
                    if (!result.IsSuccess)
                        _logger.LogError("HandlePaymentFailed failed for {Id}: {Errors}", paymentIntent.Id, string.Join(", ", result.Errors));

                    break;
                }

                default:
                    _logger.LogInformation("Unhandled Stripe event type: {EventType}", stripeEvent.Type);
                    break;
            }

            // Always return 200 to Stripe — otherwise it will keep retrying
            return Ok();
        }
    }
}
