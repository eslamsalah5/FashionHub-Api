namespace Application.DTOs.Orders
{    public class OrderItemDto
    {
        public int ProductId { get; set; }
        public required string ProductName { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal Subtotal { get; set; }
        public required string ProductSKU { get; set; }
        public required string SelectedSize { get; set; }
        public required string SelectedColor { get; set; }
    }
}
