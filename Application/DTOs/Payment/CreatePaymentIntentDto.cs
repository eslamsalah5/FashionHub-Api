namespace Application.DTOs.Payment
{
    public class CreatePaymentIntentDto
    {
        /// <summary>
        /// Accepted in the request body but ignored — cart is resolved from the JWT customerId.
        /// </summary>
        public int CartId { get; set; }

        /// <summary>
        /// The payment gateway the customer chose on the frontend.
        /// Must match IPaymentGateway.GatewayName of a registered gateway.
        /// e.g. "stripe", "paymob"
        /// </summary>
        public string Gateway { get; set; } = "stripe";

        /// <summary>
        /// The specific payment method within the gateway (Paymob only).
        /// e.g. "card", "vodafone_cash", "orange_cash", "fawry", "meeza"
        /// Leave null for Stripe (it only has one integration).
        /// </summary>
        public string? PaymentMethod { get; set; }
    }
}
