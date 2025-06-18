namespace Application.DTOs.Payment
{
    public class PaymentIntentResponseDto
    {
        public string ClientSecret { get; set; } = string.Empty;
        
        public decimal Amount { get; set; }
    }
}
