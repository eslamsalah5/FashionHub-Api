using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Orders
{
    public class CreateOrderDto
    {
        [Required]
        public int CartId { get; set; }
        
        public required string OrderNotes { get; set; } = string.Empty;
    }
}
