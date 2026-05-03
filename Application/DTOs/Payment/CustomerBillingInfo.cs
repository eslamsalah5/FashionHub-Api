namespace Application.DTOs.Payment
{
    /// <summary>
    /// Optional billing information passed to payment gateways that require it (e.g. Paymob).
    /// All fields are optional — gateways fall back to sensible defaults when omitted.
    /// </summary>
    public class CustomerBillingInfo
    {
        public string FirstName   { get; set; } = string.Empty;
        public string LastName    { get; set; } = string.Empty;
        public string Email       { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string Address     { get; set; } = string.Empty;
        public string City        { get; set; } = "Cairo";
        public string Country     { get; set; } = "EG";
    }
}
