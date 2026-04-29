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
        /// Must match <see cref="IPaymentGateway.GatewayName"/> of a registered gateway.
        /// e.g. "stripe", "paypal", "paymob"
        /// </summary>
        public string Gateway { get; set; } = "stripe";
    }
}
