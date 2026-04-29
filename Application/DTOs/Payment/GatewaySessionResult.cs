namespace Application.DTOs.Payment
{
    /// <summary>
    /// Normalised result returned by any payment gateway after creating a session.
    /// The frontend uses <see cref="ClientSecret"/> (or equivalent token) to complete payment.
    /// </summary>
    public class GatewaySessionResult
    {
        /// <summary>
        /// The token / client secret the frontend passes to the gateway SDK.
        /// For Stripe this is the PaymentIntent clientSecret.
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// The gateway's own ID for this payment session (e.g. Stripe PaymentIntent ID).
        /// Stored in the Payment record so the webhook can look it up.
        /// </summary>
        public string GatewayPaymentId { get; set; } = string.Empty;

        /// <summary>Amount in the original currency (dollars, not cents).</summary>
        public decimal Amount { get; set; }
    }
}
