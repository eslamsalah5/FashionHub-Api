using Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Orders
{
    public class UpdateOrderStatusDto
    {
        [Required]
        public OrderStatus Status { get; set; }
    }
}
