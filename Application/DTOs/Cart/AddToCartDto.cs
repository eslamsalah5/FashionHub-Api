namespace Application.DTOs.Cart
{
    public class AddToCartDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; } = 1;
        public string SelectedSize { get; set; } = string.Empty;
        public string SelectedColor { get; set; } = string.Empty;
    }
}
