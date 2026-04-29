using System;

namespace Domain.Entities
{
    public class Payment : BaseEntity<int>
    {
        public decimal Amount { get; set; }
        
        public string StripePaymentIntentId { get; set; } = string.Empty;
        
        public string Status { get; set; } = "pending"; // pending, succeeded, failed
        
        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Stored at intent creation so the webhook can create the order
        /// without relying on the client to send the customerId.
        /// </summary>
        public string CustomerId { get; set; } = string.Empty;
    }
}
