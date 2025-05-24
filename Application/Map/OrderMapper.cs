using Application.DTOs.Orders;
using Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace Application.Map
{
    public static class OrderMapper
    {        public static OrderDto ToOrderDto(this Order order)
        {
            return new OrderDto
            {
                CustomerName = order.Customer?.AppUser?.FullName ?? "Unknown Customer",                
                OrderDate = order.OrderDate,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                OrderNotes = order.OrderNotes,
                OrderItems = order.OrderItems?.Select(item => item.ToOrderItemDto()).ToList() ?? new List<OrderItemDto>()
            };
        }

        public static List<OrderDto> ToOrderDtoList(this IEnumerable<Order> orders)
        {
            return orders.Select(order => order.ToOrderDto()).ToList();
        }        public static OrderItemDto ToOrderItemDto(this OrderItem orderItem)
        {
            return new OrderItemDto
            {
                ProductId = orderItem.ProductId,
                ProductName = orderItem.ProductName,
                UnitPrice = orderItem.UnitPrice,
                Quantity = orderItem.Quantity,
                Subtotal = orderItem.Subtotal,
                ProductSKU = orderItem.ProductSKU,
                SelectedSize = orderItem.SelectedSize,
                SelectedColor = orderItem.SelectedColor
            };
        }}
}
