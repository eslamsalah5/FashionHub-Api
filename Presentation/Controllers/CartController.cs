using Application.DTOs.Cart;
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
    [Authorize]
    public class CartController : ControllerBase
    {
        private readonly ICartService _cartService;

        public CartController(ICartService cartService)
        {
            _cartService = cartService;
        }        [HttpGet]
        public async Task<ActionResult<ApiResponse>> GetCart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new ApiResponse(401, "User not authorized"));
            }

            var result = await _cartService.GetCartAsync(userId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error retrieving cart"));

            return Ok(new ApiResponse(200, result.Data));
        }

        [HttpPost("items")]
        public async Task<ActionResult<ApiResponse>> AddToCart(AddToCartDto addToCartDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _cartService.AddToCartAsync(userId, addToCartDto);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error adding item to cart"));

            return Ok(new ApiResponse(200, result.Data, "Item added to cart successfully"));
        }        [HttpPut("items")]
        public async Task<ActionResult<ApiResponse>> UpdateCartItem(UpdateCartItemDto updateCartItemDto)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _cartService.UpdateCartItemQuantityAsync(userId, updateCartItemDto);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error updating cart item"));

            return Ok(new ApiResponse(200, result.Data, "Cart item updated successfully"));
        }
        
        [HttpPut("items/{cartItemId}/increase")]
        public async Task<ActionResult<ApiResponse>> IncreaseCartItemQuantity(int cartItemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _cartService.IncreaseCartItemQuantityAsync(userId, cartItemId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error increasing item quantity"));

            return Ok(new ApiResponse(200, result.Data, "Item quantity increased successfully"));
        }
        
        [HttpPut("items/{cartItemId}/decrease")]
        public async Task<ActionResult<ApiResponse>> DecreaseCartItemQuantity(int cartItemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _cartService.DecreaseCartItemQuantityAsync(userId, cartItemId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error decreasing item quantity"));

            return Ok(new ApiResponse(200, result.Data, "Item quantity decreased successfully"));
        }

        [HttpDelete("items/{cartItemId}")]
        public async Task<ActionResult<ApiResponse>> RemoveCartItem(int cartItemId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _cartService.RemoveCartItemAsync(userId, cartItemId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error removing item from cart"));

            return Ok(new ApiResponse(200, result.Data, "Item removed from cart successfully"));
        }

        [HttpDelete("clear")]
        public async Task<ActionResult<ApiResponse>> ClearCart()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _cartService.ClearCartAsync(userId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error clearing cart"));

            return Ok(new ApiResponse(200, null, "Cart cleared successfully"));
        }

        [HttpGet("count")]
        public async Task<ActionResult<ApiResponse>> GetCartItemCount()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _cartService.GetCartItemCountAsync(userId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error retrieving cart count"));

            return Ok(new ApiResponse(200, result.Data));
        }

        [HttpGet("check-product/{productId}")]
        public async Task<ActionResult<ApiResponse>> IsProductInCart(int productId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var result = await _cartService.IsProductInCartAsync(userId, productId);

            if (!result.IsSuccess)
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Error checking product status"));

            return Ok(new ApiResponse(200, result.Data));
        }
    }
}
