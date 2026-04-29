namespace Application.DTOs.Payment
{
    /// <summary>
    /// Describes a single available payment gateway and its supported methods.
    /// Returned by GET /api/payment/methods so the frontend can build the payment UI.
    /// </summary>
    public class PaymentGatewayDto
    {
        /// <summary>Gateway identifier — pass this as "gateway" in the checkout request.</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Human-readable name for display in the UI.</summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Available payment methods for this gateway.
        /// Null means the gateway has a single method (e.g. Stripe card).
        /// For Paymob: ["card", "wallet"]
        /// </summary>
        public List<PaymentMethodDto>? Methods { get; set; }
    }

    public class PaymentMethodDto
    {
        /// <summary>Method key — pass this as "paymentMethod" in the checkout request.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Human-readable label for display in the UI.</summary>
        public string DisplayName { get; set; } = string.Empty;
    }
}
