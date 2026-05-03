using Application.DTOs.Payment;
using Application.Models;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Stripe;

namespace Infrastructure.ExternalServices.PaymentGateways
{
    /// <summary>
    /// Stripe implementation of <see cref="IPaymentGateway"/>.
    /// All Stripe-specific code lives here — nothing else in the codebase imports Stripe directly.
    /// </summary>
    public class StripePaymentGateway : IPaymentGateway
    {
        private readonly PaymentIntentService _paymentIntentService;
        private readonly string _webhookSecret;

        public string GatewayName => "stripe";

        public StripePaymentGateway(PaymentIntentService paymentIntentService, string webhookSecret)
        {
            _paymentIntentService = paymentIntentService;
            _webhookSecret        = webhookSecret;
        }

        // ─────────────────────────────────────────────────────────────
        // Create a Stripe PaymentIntent and return the clientSecret
        // ─────────────────────────────────────────────────────────────
        public async Task<ServiceResult<GatewaySessionResult>> CreateSessionAsync(
            decimal amount, string currency, string customerId,
            string? paymentMethod = null,
            CustomerBillingInfo? billingInfo = null)
        {
            try
            {
                var options = new PaymentIntentCreateOptions
                {
                    Amount             = (long)(amount * 100), // Stripe works in cents
                    Currency           = currency,
                    PaymentMethodTypes = new List<string> { "card" },
                    Metadata           = new Dictionary<string, string>
                    {
                        { "customerId", customerId }
                    }
                };

                var intent = await _paymentIntentService.CreateAsync(options);

                return ServiceResult<GatewaySessionResult>.Success(new GatewaySessionResult
                {
                    ClientSecret    = intent.ClientSecret,
                    GatewayPaymentId = intent.Id,
                    Amount          = amount
                });
            }
            catch (StripeException ex)
            {
                return ServiceResult<GatewaySessionResult>.Failure(
                    $"Stripe error: {ex.StripeError?.Message ?? ex.Message}");
            }
            catch (Exception ex)
            {
                return ServiceResult<GatewaySessionResult>.Failure(
                    $"Error creating payment session: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────
        // Verify the Stripe-Signature header and return a normalised event
        // ─────────────────────────────────────────────────────────────
        public Task<ServiceResult<GatewayWebhookEvent>> ParseWebhookAsync(
            string rawBody, IDictionary<string, string> headers)
        {
            if (string.IsNullOrEmpty(_webhookSecret) || _webhookSecret.StartsWith("whsec_your"))
                return Task.FromResult(
                    ServiceResult<GatewayWebhookEvent>.Failure("Webhook secret not configured"));

            headers.TryGetValue("Stripe-Signature", out var signature);
            if (string.IsNullOrEmpty(signature))
                return Task.FromResult(
                    ServiceResult<GatewayWebhookEvent>.Failure(
                        "Webhook signature verification failed: Stripe-Signature header is missing."));

            try
            {
                var stripeEvent = EventUtility.ConstructEvent(
                    rawBody, signature, _webhookSecret,
                    throwOnApiVersionMismatch: false);

                // Map Stripe event types → normalised types
                var eventType = stripeEvent.Type switch
                {
                    "payment_intent.succeeded"      => GatewayEventTypes.PaymentSucceeded,
                    "payment_intent.payment_failed" => GatewayEventTypes.PaymentFailed,
                    _                               => stripeEvent.Type   // pass through unknown types
                };

                var paymentIntent = stripeEvent.Data.Object as PaymentIntent;

                return Task.FromResult(ServiceResult<GatewayWebhookEvent>.Success(
                    new GatewayWebhookEvent
                    {
                        EventType        = eventType,
                        GatewayPaymentId = paymentIntent?.Id ?? string.Empty,
                        EventId          = stripeEvent.Id
                    }));
            }
            catch (StripeException ex)
            {
                return Task.FromResult(
                    ServiceResult<GatewayWebhookEvent>.Failure(
                        $"Webhook signature verification failed: {ex.Message}"));
            }
        }
    }
}
