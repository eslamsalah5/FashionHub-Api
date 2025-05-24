using Application.DTOs.Orders;
using Application.Models;
using Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Services.Interfaces
{
    public interface IOrderService
    {
        Task<ServiceResult<OrderDto>> CreateOrderAsync(string userId, CreateOrderDto createOrderDto);
        Task<ServiceResult<OrderDto>> GetOrderByIdAsync(int orderId, string userId);
        Task<ServiceResult<List<OrderDto>>> GetUserOrdersAsync(string userId);
        Task<ServiceResult<OrderDto>> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusDto updateOrderDto, string userId);
        Task<ServiceResult<PagedResult<OrderDto>>> GetOrdersAsync(int page, int pageSize, OrderStatus? status = null);
    }
}
