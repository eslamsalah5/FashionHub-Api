using Application.DTOs.Orders;
using Application.Map;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Services
{
    public class OrderService : IOrderService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<OrderService> _logger;

        public OrderService(IUnitOfWork unitOfWork, ILogger<OrderService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<ServiceResult<OrderDto>> CreateOrderAsync(string userId, CreateOrderDto createOrderDto)
        {
            try
            {
                var validationResult = await ValidateCartAndCustomer(userId, createOrderDto.CartId);
                if (!validationResult.IsSuccess)
                    return ServiceResult<OrderDto>.Failure(validationResult.Errors.First());
                
                var order = await _unitOfWork.Orders.CreateOrderFromCartAsync(createOrderDto.CartId);
                if (order == null)
                    return ServiceResult<OrderDto>.Failure("Failed to create order from cart.");
                    
                order.OrderNotes = createOrderDto.OrderNotes;

                await _unitOfWork.SaveChangesAsync();

                return ServiceResult<OrderDto>.Success(order.ToOrderDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order for user {UserId}", userId);
                return ServiceResult<OrderDto>.Failure("An error occurred while creating the order. Please try again.");
            }
        }

        private async Task<ServiceResult<bool>> ValidateCartAndCustomer(string userId, int cartId)
        {
            var customer = await _unitOfWork.Customers.GetByIdAsync(userId);
            if (customer == null)
                return ServiceResult<bool>.Failure("Customer not found.");

            var cart = await _unitOfWork.Carts.GetCartWithItemsByIdAsync(cartId);
            if (cart == null)
                return ServiceResult<bool>.Failure("Cart not found.");

            if (cart.CustomerId != userId)
                return ServiceResult<bool>.Failure("Unauthorized access to cart.");

            if (!cart.CartItems.Any())
                return ServiceResult<bool>.Failure("Cannot create order with empty cart.");
                
            return ServiceResult<bool>.Success(true);
        }

        public async Task<ServiceResult<OrderDto>> GetOrderByIdAsync(int orderId, string userId)
        {
            try
            {
                var accessResult = await VerifyOrderAccess(orderId, userId);
                if (!accessResult.IsSuccess)
                    return ServiceResult<OrderDto>.Failure(accessResult.Errors.First());
                
                var order = await _unitOfWork.Orders.GetOrderWithItemsAsync(orderId);
                if (order == null)
                    return ServiceResult<OrderDto>.Failure("Order not found.");

                return ServiceResult<OrderDto>.Success(order.ToOrderDto());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {OrderId} for user {UserId}", orderId, userId);
                return ServiceResult<OrderDto>.Failure("An error occurred while retrieving the order.");
            }
        }

        public async Task<ServiceResult<List<OrderDto>>> GetUserOrdersAsync(string userId)
        {
            try
            {
                var orders = await _unitOfWork.Orders.GetCustomerOrdersAsync(userId);
                
                return ServiceResult<List<OrderDto>>.Success(orders.ToOrderDtoList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders for user {UserId}", userId);
                return ServiceResult<List<OrderDto>>.Failure("An error occurred while retrieving your orders.");
            }
        }

        public async Task<ServiceResult<OrderDto>> UpdateOrderStatusAsync(int orderId, UpdateOrderStatusDto updateOrderDto, string userId)
        {
            try
            {
                var validationResult = await ValidateAdminAndOrder(userId, orderId);
                if (!validationResult.IsSuccess)
                    return ServiceResult<OrderDto>.Failure(validationResult.Errors.First());
                
                var order = validationResult.Data!;
                order.Status = updateOrderDto.Status;

                await _unitOfWork.SaveChangesAsync();

                var updatedOrder = await _unitOfWork.Orders.GetOrderWithItemsAsync(orderId);
                return updatedOrder != null 
                    ? ServiceResult<OrderDto>.Success(updatedOrder.ToOrderDto())
                    : ServiceResult<OrderDto>.Failure("Error retrieving updated order.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status for order {OrderId}", orderId);
                return ServiceResult<OrderDto>.Failure("An error occurred while updating the order status.");
            }
        }

        private async Task<ServiceResult<Order>> ValidateAdminAndOrder(string userId, int orderId)
        {
            var admin = await _unitOfWork.Admins.GetByIdAsync(userId);
            if (admin == null)
                return ServiceResult<Order>.Failure("Unauthorized. Only administrators can update order status.");

            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                return ServiceResult<Order>.Failure("Order not found.");
                
            return ServiceResult<Order>.Success(order);
        }

        public async Task<ServiceResult<PagedResult<OrderDto>>> GetOrdersAsync(int page, int pageSize, OrderStatus? status = null)
        {
            try
            {
                var query = _unitOfWork.Orders.GetAllQueryable().AsNoTracking();
                
                if (status.HasValue)
                    query = query.Where(o => o.Status == status.Value);

                var totalCount = await query.CountAsync();

                var orders = await query
                    .OrderByDescending(o => o.OrderDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Include(o => o.OrderItems)
                    .ToListAsync();

                var result = new PagedResult<OrderDto>(
                    items: orders.ToOrderDtoList(), 
                    pageIndex: page - 1, 
                    pageSize: pageSize, 
                    totalCount: totalCount);

                return ServiceResult<PagedResult<OrderDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting paginated orders");
                return ServiceResult<PagedResult<OrderDto>>.Failure("An error occurred while retrieving the orders.");
            }
        }

        private async Task<ServiceResult<bool>> VerifyOrderAccess(int orderId, string userId)
        {
            var order = await _unitOfWork.Orders.GetByIdAsync(orderId);
            if (order == null)
                return ServiceResult<bool>.Failure("Order not found.");

            if (order.CustomerId != userId)
            {
                var admin = await _unitOfWork.Admins.GetByIdAsync(userId);
                if (admin == null)
                    return ServiceResult<bool>.Failure("Unauthorized access to order.");
            }
            
            return ServiceResult<bool>.Success(true);
        }
    }
}
