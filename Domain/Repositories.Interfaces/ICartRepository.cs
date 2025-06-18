using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Repositories.Interfaces
{
    public interface ICartRepository : IGenericRepository<Cart>
    {
        Task<Cart?> GetCartWithItemsByCustomerIdAsync(string customerId);
        Task<Cart?> GetCartWithItemsByIdAsync(int id);
        Task<Cart?> GetCartWithItemsAsync(int cartId);
        Task<bool> AddItemToCartAsync(int cartId, int productId, int quantity, string selectedSize = "", string selectedColor = "");
        Task<bool> UpdateCartItemQuantityAsync(int cartItemId, int quantity);
        Task<bool> RemoveCartItemAsync(int cartItemId);
        Task<bool> ClearCartAsync(int cartId);
        Task AddCartAsync(Cart cart);
        Task<CartItem?> GetCartItemByIdAsync(int cartItemId);
        Task<int> GetCartItemCountAsync(string customerId);
        Task<bool> IsProductInCartAsync(string customerId, int productId);
        Task<bool> IncreaseCartItemQuantityAsync(int cartItemId);
        Task<bool> DecreaseCartItemQuantityAsync(int cartItemId);
        Task<Cart> GetOrCreateCartAsync(string customerId);
    }
}
