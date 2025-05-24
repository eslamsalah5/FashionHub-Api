using System;
using System.Collections.Generic;

namespace Domain.Entities
{
    public class Cart : BaseEntity<int>
    {
        public string CustomerId { get; set; } = string.Empty;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public Customer Customer { get; set; } = null!;
        
        public ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();
    }
}
