using Application.DTOs.Cart;
using Application.Models;
using System.Threading.Tasks;

namespace Application.Services.Interfaces
{
    public interface ICartService
    {
        Task<ServiceResult<CartDto>> GetCartAsync(string userId);
        Task<ServiceResult<CartDto>> AddToCartAsync(string userId, AddToCartDto request);
        Task<ServiceResult<CartDto>> UpdateCartItemQuantityAsync(string userId, UpdateCartItemDto request);
        Task<ServiceResult<CartDto>> RemoveCartItemAsync(string userId, int cartItemId);
        Task<ServiceResult<bool>> ClearCartAsync(string userId);
        Task<ServiceResult<CartDto>> IncreaseCartItemQuantityAsync(string userId, int cartItemId);
        Task<ServiceResult<CartDto>> DecreaseCartItemQuantityAsync(string userId, int cartItemId);
        Task<ServiceResult<int>> GetCartItemCountAsync(string userId);
        Task<ServiceResult<bool>> IsProductInCartAsync(string userId, int productId);
    }
}
