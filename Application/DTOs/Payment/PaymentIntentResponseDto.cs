namespace Application.DTOs.Payment
{
    /// <summary>
    /// Returned to the frontend after a payment session is created.
    /// </summary>
    public class PaymentIntentResponseDto
    {
        /// <summary>
        /// Token to complete the payment.
        /// - Stripe: pass to Stripe.js confirmCardPayment()
        /// - Paymob: use with PublicKey to redirect to Unified Checkout
        /// </summary>
        public string ClientSecret { get; set; } = string.Empty;

        /// <summary>Amount in the original currency (not cents).</summary>
        public decimal Amount { get; set; }

        /// <summary>
        /// Paymob only: Public Key for the Unified Checkout.
        /// Frontend redirect:
        ///   https://accept.paymob.com/unifiedcheckout/?publicKey={PublicKey}&clientSecret={ClientSecret}
        /// </summary>
        public string? PublicKey { get; set; }

        /// <summary>Which gateway processed this session: "stripe" | "paymob"</summary>
        public string Gateway { get; set; } = string.Empty;
    }
}
