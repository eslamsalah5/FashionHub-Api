using Application.DTOs.Orders;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Errors;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;

        public OrdersController(IOrderService orderService)
        {
            _orderService = orderService;
        }

        [HttpPost]
        [Authorize(Roles = "Customer")]
        public async Task<ActionResult<ApiResponse>> CreateOrder(CreateOrderDto createOrderDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authorized"));

            var result = await _orderService.CreateOrderAsync(userId, createOrderDto);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error creating order"));

            return Ok(new ApiResponse(200, result.Data, "Order created successfully"));
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<ApiResponse>> GetOrderById(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authorized"));

            var result = await _orderService.GetOrderByIdAsync(id, userId);

            if (!result.IsSuccess)
                return NotFound(new ApiResponse(404, result.Errors.FirstOrDefault() ?? "Order not found"));

            return Ok(new ApiResponse(200, result.Data));
        }

        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<ActionResult<ApiResponse>> GetMyOrders()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return Unauthorized(new ApiResponse(401, "User not authorized"));

            var result = await _orderService.GetUserOrdersAsync(userId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error retrieving orders"));

            return Ok(new ApiResponse(200, result.Data));
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse>> UpdateOrderStatus(int id, UpdateOrderStatusDto updateOrderDto)
        {
            var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(adminId))
                return Unauthorized(new ApiResponse(401, "Admin not authorized"));

            var result = await _orderService.UpdateOrderStatusAsync(id, updateOrderDto, adminId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error updating order status"));

            return Ok(new ApiResponse(200, result.Data, "Order status updated successfully"));
        }        [HttpGet("admin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse>> GetAllOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _orderService.GetOrdersAsync(page, pageSize);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error retrieving orders"));

            return Ok(new ApiResponse(200, result.Data));
        }
    }
}
