using Application.DTOs.Payment;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly IEnumerable<IPaymentGateway> _gateways;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IPaymentService paymentService,
            IEnumerable<IPaymentGateway> gateways,
            ILogger<PaymentController> logger)
        {
            _paymentService = paymentService;
            _gateways       = gateways;
            _logger         = logger;
        }

        // ─────────────────────────────────────────────────────────────
        // GET api/payment/methods
        // Returns all available payment gateways and their methods.
        // The frontend uses this to build the payment selection UI.
        // ─────────────────────────────────────────────────────────────
        [HttpGet("methods")]
        [AllowAnonymous]
        public IActionResult GetPaymentMethods()
        {
            var result = _gateways.Select(g => new PaymentGatewayDto
            {
                Name        = g.GatewayName,
                DisplayName = g.GatewayName switch
                {
                    "stripe" => "Credit / Debit Card",
                    "paymob" => "Paymob",
                    _        => g.GatewayName
                },
                Methods = g.GatewayName switch
                {
                    "paymob" => new List<PaymentMethodDto>
                    {
                        new() { Key = "card",   DisplayName = "Visa / Mastercard / Meeza" },
                        new() { Key = "wallet", DisplayName = "Mobile Wallet (Vodafone Cash, Orange Cash, e& money)" }
                    },
                    _ => null
                }
            }).ToList();

            return Ok(result);
        }

        // ─────────────────────────────────────────────────────────────
        // POST api/payment/create-payment-intent
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
        // POST api/payment/webhook/{gateway}
        // Each gateway has its own webhook endpoint so Stripe, PayPal, etc.
        // can each be configured with their own URL in the gateway dashboard.
        // e.g. POST /api/payment/webhook/stripe
        //      POST /api/payment/webhook/paypal
        // ─────────────────────────────────────────────────────────────
        [HttpPost("webhook/{gateway}")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook([FromRoute] string gateway)
        {
            // Resolve the gateway
            var paymentGateway = _gateways.FirstOrDefault(
                g => g.GatewayName.Equals(gateway, StringComparison.OrdinalIgnoreCase));

            if (paymentGateway == null)
            {
                _logger.LogWarning("Webhook received for unknown gateway: {Gateway}", gateway);
                return BadRequest($"Unknown payment gateway: {gateway}");
            }

            // Read raw body — required for signature verification
            string rawBody;
            using (var reader = new StreamReader(HttpContext.Request.Body))
            {
                rawBody = await reader.ReadToEndAsync();
            }

            // Delegate signature verification + event parsing to the gateway
            // Convert IHeaderDictionary → IDictionary<string, string> for the gateway interface
            var headers = Request.Headers
                .Where(h => h.Value.Count > 0)
                .ToDictionary(
                    h => h.Key,
                    h => h.Value.ToString(),
                    StringComparer.OrdinalIgnoreCase);

            // Paymob sends the HMAC as a query parameter (?hmac=...) — inject it into
            // the headers dict so ParseWebhookAsync can find it in one place.
            if (Request.Query.TryGetValue("hmac", out var hmacQuery) &&
                !string.IsNullOrEmpty(hmacQuery))
            {
                headers["hmac"] = hmacQuery.ToString();
            }

            var parseResult = await paymentGateway.ParseWebhookAsync(rawBody, headers);

            if (!parseResult.IsSuccess)
            {
                var error = string.Join(", ", parseResult.Errors);

                if (error.Contains("not configured"))
                {
                    _logger.LogError("{Gateway} webhook secret is not configured.", gateway);
                    return StatusCode(500, parseResult.Errors.First());
                }

                _logger.LogWarning("{Gateway} webhook verification failed: {Error}", gateway, error);
                return BadRequest(error);
            }

            var webhookEvent = parseResult.Data!;
            _logger.LogInformation(
                "{Gateway} webhook received: {EventType} | {EventId}",
                gateway, webhookEvent.EventType, webhookEvent.EventId);

            // Dispatch to the correct handler based on the normalised event type
            switch (webhookEvent.EventType)
            {
                case GatewayEventTypes.PaymentSucceeded:
                {
                    var result = await _paymentService.HandlePaymentSucceededAsync(webhookEvent);

                    if (!result.IsSuccess)
                        _logger.LogError(
                            "HandlePaymentSucceeded failed for {Id}: {Errors}",
                            webhookEvent.GatewayPaymentId,
                            string.Join(", ", result.Errors));
                    break;
                }

                case GatewayEventTypes.PaymentFailed:
                {
                    var result = await _paymentService.HandlePaymentFailedAsync(webhookEvent);

                    if (!result.IsSuccess)
                        _logger.LogError(
                            "HandlePaymentFailed failed for {Id}: {Errors}",
                            webhookEvent.GatewayPaymentId,
                            string.Join(", ", result.Errors));
                    break;
                }

                default:
                    _logger.LogInformation(
                        "Unhandled {Gateway} event type: {EventType}",
                        gateway, webhookEvent.EventType);
                    break;
            }

            // Always return 200 — prevents the gateway from retrying
            return Ok();
        }

        // ─────────────────────────────────────────────────────────────
        // POST api/payment/force-success/{gatewayPaymentId}
        // Temporary endpoint for debugging to prove the order creation logic works.
        // Bypasses the Stripe webhook signature verification.
        // ─────────────────────────────────────────────────────────────
        [HttpPost("force-success/{gatewayPaymentId}")]
        [AllowAnonymous]
        public async Task<IActionResult> ForceSuccess([FromRoute] string gatewayPaymentId)
        {
            var dummyEvent = new GatewayWebhookEvent
            {
                EventType        = GatewayEventTypes.PaymentSucceeded,
                GatewayPaymentId = gatewayPaymentId,
                EventId          = "evt_test_forced"
            };

            var result = await _paymentService.HandlePaymentSucceededAsync(dummyEvent);
            if (!result.IsSuccess)
                return BadRequest(string.Join(", ", result.Errors));

            return Ok(new { Message = "Order created successfully via forced webhook!", OrderId = result.Data });
        }
    }
}
