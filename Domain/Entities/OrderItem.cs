namespace Domain.Entities
{
    public class OrderItem : BaseEntity<int>
    {
        public int OrderId { get; set; }
        
        public int ProductId { get; set; }
        
        public string ProductName { get; set; } = string.Empty;
        
        public decimal UnitPrice { get; set; }
        
        public int Quantity { get; set; } = 1;
        
        public decimal Subtotal { get; set; }
        
        public string ProductSKU { get; set; } = string.Empty;
        
        public string SelectedSize { get; set; } = string.Empty;
        
        public string SelectedColor { get; set; } = string.Empty;
        
        // Navigation properties
        public Order Order { get; set; } = null!;
        
        public Product Product { get; set; } = null!;
    }
}
