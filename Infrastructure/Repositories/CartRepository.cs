using Domain.Entities;
using Domain.Repositories.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{    public class CartRepository : GenericRepository<Cart>, ICartRepository
    {

        public CartRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<Cart?> GetCartWithItemsByCustomerIdAsync(string customerId)
        {
            return await _context.Carts
                .Where(c => c.CustomerId == customerId && !c.IsDeleted)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync();
        }        public async Task<Cart?> GetCartWithItemsByIdAsync(int id)
        {
            return await _context.Carts
                .Where(c => c.Id == id && !c.IsDeleted)
                .Include(c => c.CartItems)
                    .ThenInclude(ci => ci.Product)
                .FirstOrDefaultAsync();
        }
        
        public async Task<Cart?> GetCartWithItemsAsync(int cartId)
        {
            return await GetCartWithItemsByIdAsync(cartId);
        }

        public async Task<bool> AddItemToCartAsync(int cartId, int productId, int quantity)
        {
            // Check if the product already exists in the cart
            var existingCartItem = await _context.Set<CartItem>()
                .FirstOrDefaultAsync(ci => ci.CartId == cartId && ci.ProductId == productId);

            if (existingCartItem != null)
            {
                // Update quantity if the product already exists in the cart
                existingCartItem.Quantity += quantity;
            }
            else
            {
                // Get the product for the price
                var product = await _context.Products.FindAsync(productId);
                if (product == null) return false;

                // Add a new cart item
                var newCartItem = new CartItem
                {
                    CartId = cartId,
                    ProductId = productId,
                    Quantity = quantity,
                    PriceAtAddition = product.IsOnSale && product.DiscountPrice.HasValue 
                        ? product.DiscountPrice.Value 
                        : product.Price
                };

                await _context.Set<CartItem>().AddAsync(newCartItem);
            }

            // Update the cart's ModifiedAt timestamp
            var cart = await _context.Carts.FindAsync(cartId);
            if (cart == null) return false;
            
            cart.ModifiedAt = DateTime.UtcNow;
            return true;
        }

        public async Task<bool> UpdateCartItemQuantityAsync(int cartItemId, int quantity)
        {
            var cartItem = await _context.Set<CartItem>().FindAsync(cartItemId);
            if (cartItem == null) return false;

            if (quantity <= 0)
            {
                _context.Set<CartItem>().Remove(cartItem);
            }
            else
            {
                cartItem.Quantity = quantity;
            }

            // Update the cart's ModifiedAt timestamp
            var cart = await _context.Carts.FindAsync(cartItem.CartId);
            if (cart == null) return false;
            
            cart.ModifiedAt = DateTime.UtcNow;
            return true;
        }

        public async Task<bool> RemoveCartItemAsync(int cartItemId)
        {
            var cartItem = await _context.Set<CartItem>().FindAsync(cartItemId);
            if (cartItem == null) return false;

            // Update the cart's ModifiedAt timestamp
            var cart = await _context.Carts.FindAsync(cartItem.CartId);
            if (cart == null) return false;
            
            cart.ModifiedAt = DateTime.UtcNow;

            _context.Set<CartItem>().Remove(cartItem);
            return true;
        }

        public async Task<bool> ClearCartAsync(int cartId)
        {
            var cartItems = await _context.Set<CartItem>()
                .Where(ci => ci.CartId == cartId)
                .ToListAsync();

            if (!cartItems.Any()) return false;

            _context.Set<CartItem>().RemoveRange(cartItems);
            
            // Update the cart's ModifiedAt timestamp
            var cart = await _context.Carts.FindAsync(cartId);
            if (cart == null) return false;
            
            cart.ModifiedAt = DateTime.UtcNow;
            return true;
        }

        public async Task AddCartAsync(Cart cart)
        {
            await _context.Carts.AddAsync(cart);
        }

        public async Task<CartItem?> GetCartItemByIdAsync(int cartItemId)
        {
            return await _context.Set<CartItem>()
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId);
        }

        public async Task<int> GetCartItemCountAsync(string customerId)
        {
            var cart = await GetCartWithItemsByCustomerIdAsync(customerId);
            if (cart == null || cart.CartItems == null || !cart.CartItems.Any())
            {
                return 0;
            }

            return cart.CartItems.Sum(ci => ci.Quantity);
        }

        public async Task<bool> IsProductInCartAsync(string customerId, int productId)
        {
            var cart = await GetCartWithItemsByCustomerIdAsync(customerId);
            if (cart == null || cart.CartItems == null)
            {
                return false;
            }

            return cart.CartItems.Any(ci => ci.ProductId == productId);
        }

        public async Task<bool> IncreaseCartItemQuantityAsync(int cartItemId)
        {
            var cartItem = await _context.Set<CartItem>()
                .Include(ci => ci.Product)
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId);

            if (cartItem == null)
                return false;

            // Check stock availability
            if (cartItem.Product.StockQuantity <= cartItem.Quantity)
                return false;

            // Increase quantity by 1
            cartItem.Quantity++;
            
            // Update the cart's ModifiedAt timestamp
            var cart = await _context.Carts.FindAsync(cartItem.CartId);
            if (cart == null) return false;
            
            cart.ModifiedAt = DateTime.UtcNow;
            return true;
        }

        public async Task<bool> DecreaseCartItemQuantityAsync(int cartItemId)
        {
            var cartItem = await _context.Set<CartItem>()
                .FirstOrDefaultAsync(ci => ci.Id == cartItemId);

            if (cartItem == null)
                return false;

            if (cartItem.Quantity <= 1)
            {
                // Remove the item instead
                return await RemoveCartItemAsync(cartItemId);
            }

            // Decrease quantity by 1
            cartItem.Quantity--;
            
            // Update the cart's ModifiedAt timestamp
            var cart = await _context.Carts.FindAsync(cartItem.CartId);
            if (cart == null) return false;
            
            cart.ModifiedAt = DateTime.UtcNow;
            return true;
        }

        public async Task<Cart> GetOrCreateCartAsync(string customerId)
        {
            // Get existing active cart
            var existingCart = await GetCartWithItemsByCustomerIdAsync(customerId);
            
            if (existingCart != null)
            {
                return existingCart;
            }
            
            // Create new cart if none exists
            var newCart = new Cart
            {
                CustomerId = customerId,
                CreatedAt = DateTime.UtcNow,
                ModifiedAt = DateTime.UtcNow
            };
            
            await AddCartAsync(newCart);
            
            return newCart;
        }
    }
}
