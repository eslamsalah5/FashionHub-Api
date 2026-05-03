namespace Application.DTOs.Payment
{
    /// <summary>
    /// Normalised webhook event returned by any payment gateway.
    /// </summary>
    public class GatewayWebhookEvent
    {
        /// <summary>
        /// Normalised event type.
        /// Use the constants defined in <see cref="GatewayEventTypes"/>.
        /// </summary>
        public string EventType { get; set; } = string.Empty;

        /// <summary>
        /// The gateway's own payment/session ID (e.g. Stripe PaymentIntent ID).
        /// Used to look up the Payment record in the database.
        /// </summary>
        public string GatewayPaymentId { get; set; } = string.Empty;

        /// <summary>Raw event ID from the gateway (for logging).</summary>
        public string EventId { get; set; } = string.Empty;

        /// <summary>
        /// Optional: customerId echoed back by the gateway (Paymob only).
        /// Used as a fallback in PaymentService when GatewayPaymentId lookup fails.
        /// </summary>
        public string? CustomerId { get; set; }
    }

    /// <summary>
    /// Normalised event type constants — gateway-agnostic.
    /// </summary>
    public static class GatewayEventTypes
    {
        public const string PaymentSucceeded = "payment.succeeded";
        public const string PaymentFailed    = "payment.failed";
    }
}
