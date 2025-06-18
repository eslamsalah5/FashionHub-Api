using Domain.Enums;
using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Order : BaseEntity<int>
    {        public string CustomerId { get; set; } = string.Empty;
        
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        
        public OrderStatus Status { get; set; } = OrderStatus.Pending;

        public decimal TotalAmount { get; set; }
          public string OrderNotes { get; set; } = string.Empty; 
        
        public int? PaymentId { get; set; }
          // Navigation properties
        public Customer Customer { get; set; } = null!;
        
        public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
        
        public Payment? Payment { get; set; }
        
    }
}
