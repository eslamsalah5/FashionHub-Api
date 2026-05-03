namespace Domain.Entities
{
    public class Payment : BaseEntity<int>
    {
        public decimal Amount { get; set; }

        /// <summary>
        /// JSON snapshot of the cart items at intent creation time.
        /// Used by webhook processing to create orders without relying on live cart state.
        /// </summary>
        public string CartSnapshotJson { get; set; } = string.Empty;

        /// <summary>
        /// The gateway's own ID for this payment session (e.g. Stripe PaymentIntent ID).
        /// Used by the webhook handler to look up this record.
        /// </summary>
        public string GatewayPaymentId { get; set; } = string.Empty;

        /// <summary>
        /// Which gateway processed this payment (e.g. "stripe", "paypal").
        /// Allows the webhook router to dispatch to the correct gateway.
        /// </summary>
        public string GatewayName { get; set; } = string.Empty;

        public string Status { get; set; } = "pending"; // pending, succeeded, failed

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Stored at intent creation so the webhook can create the order
        /// without relying on the client to send the customerId.
        /// </summary>
        public string CustomerId { get; set; } = string.Empty;
    }
}
