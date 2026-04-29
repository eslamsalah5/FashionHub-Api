namespace Application.DTOs.Payment
{
    /// <summary>
    /// Normalised result returned by any payment gateway after creating a session.
    /// </summary>
    public class GatewaySessionResult
    {
        /// <summary>
        /// The token the frontend uses to complete payment.
        /// - Stripe: PaymentIntent clientSecret (for Stripe.js)
        /// - Paymob: client_secret (for Unified Checkout redirect)
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>
        /// The gateway's own ID for this payment session.
        /// Stored in the Payment record so the webhook can look it up.
        /// </summary>
        public string GatewayPaymentId { get; set; } = string.Empty;

        /// <summary>Amount in the original currency (not cents).</summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Paymob only: the Public Key needed by the frontend to open Unified Checkout.
        /// Frontend redirect URL:
        ///   https://accept.paymob.com/unifiedcheckout/?publicKey={PublicKey}&clientSecret={ClientSecret}
        /// Null for other gateways.
        /// </summary>
        public string? PublicKey { get; set; }

        /// <summary>
        /// Paymob legacy only: iFrame ID (not used in Unified Checkout).
        /// Kept for backward compatibility.
        /// </summary>
        public string? IframeId { get; set; }
    }
}
