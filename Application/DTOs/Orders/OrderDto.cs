using Domain.Enums;
using System;
using System.Collections.Generic;

namespace Application.DTOs.Orders
{
    public class OrderDto
    {
        public required string CustomerName { get; set; }
        public DateTime OrderDate { get; set; }
        public OrderStatus Status { get; set; }
        public decimal TotalAmount { get; set; }
        
        public required string OrderNotes { get; set; }
        public required List<OrderItemDto> OrderItems { get; set; }
    }
}
