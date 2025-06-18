namespace Domain.Entities
{
    public class CartItem : BaseEntity<int>
    {
        public int CartId { get; set; }
        
        public int ProductId { get; set; }
        
        public int Quantity { get; set; } = 1;
        
        // Optional: Store the price at the time of adding to cart
        public decimal PriceAtAddition { get; set; }
        
        public string SelectedSize { get; set; } = string.Empty;
        
        public string SelectedColor { get; set; } = string.Empty;
        
        // Navigation properties
        public Cart Cart { get; set; } = null!;
        
        public Product Product { get; set; } = null!;
    }
}
