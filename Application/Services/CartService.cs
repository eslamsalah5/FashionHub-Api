using Application.DTOs.Cart;
using Application.Map;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Application.Services
{
    public class CartService : ICartService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<CartService> _logger;

        public CartService(IUnitOfWork unitOfWork, ILogger<CartService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }        public async Task<ServiceResult<CartDto>> GetCartAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    _logger.LogWarning("Attempted to get cart with empty user ID");
                    return ServiceResult<CartDto>.Failure("User ID is required.");
                }
                
                // Log the actual user ID we're trying to find
                _logger.LogInformation("Attempting to retrieve cart for user ID: {UserId}", userId);
                
                // Find the customer first to provide better error messages
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found for user ID: {UserId}", userId);
                    return ServiceResult<CartDto>.Failure("Customer account not found. Please set up your customer profile first.");
                }
                
                // Get user's cart (moved to repository)
                var cart = await _unitOfWork.Carts.GetOrCreateCartAsync(customer.Id);
                
                // Map to DTO safely
                var cartDto = cart.ToCartDto();
                
                return ServiceResult<CartDto>.Success(cartDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cart for user {UserId}", userId);
                return ServiceResult<CartDto>.Failure("Failed to retrieve cart. Please try again later.");
            }
        }        public async Task<ServiceResult<CartDto>> AddToCartAsync(string userId, AddToCartDto request)
        {
            try
            {
                // Validate product exists and is in stock
                var product = await _unitOfWork.Products.GetByIdAsync(request.ProductId);
                if (product == null)
                {
                    return ServiceResult<CartDto>.Failure("Product not found.");
                }

                if (product.StockQuantity < request.Quantity)
                {
                    return ServiceResult<CartDto>.Failure($"Not enough stock available. Only {product.StockQuantity} items left.");
                }

                // Find customer from user ID
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    _logger.LogWarning("Customer not found for user ID: {UserId}", userId);
                    return ServiceResult<CartDto>.Failure("Customer account not found");
                }

                // Get user's cart or create a new one (moved to repository)
                var cart = await _unitOfWork.Carts.GetOrCreateCartAsync(customer.Id);

                // Add item to cart
                await _unitOfWork.Carts.AddItemToCartAsync(cart.Id, request.ProductId, request.Quantity);
                
                // Save changes
                await _unitOfWork.SaveChangesAsync();
                
                // Refresh cart with updated items
                var updatedCart = await _unitOfWork.Carts.GetCartWithItemsByIdAsync(cart.Id);
                
                var cartDto = updatedCart != null 
                    ? updatedCart.ToCartDto() 
                    : new CartDto { Id = cart.Id, CustomerId = cart.CustomerId };
                    
                return ServiceResult<CartDto>.Success(cartDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding item to cart for user {UserId}", userId);
                return ServiceResult<CartDto>.Failure("Failed to add item to cart. Please try again later.");
            }
        }        public async Task<ServiceResult<CartDto>> UpdateCartItemQuantityAsync(string userId, UpdateCartItemDto request)
        {
            try
            {
                // Find customer from user ID
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    return ServiceResult<CartDto>.Failure("Customer account not found");
                }
                
                // Get the cart
                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customer.Id);
                if (cart == null)
                {
                    return ServiceResult<CartDto>.Failure("Cart not found");
                }
                
                // Get the specific cart item
                var cartItem = await _unitOfWork.Carts.GetCartItemByIdAsync(request.CartItemId);
                if (cartItem == null)
                {
                    return ServiceResult<CartDto>.Failure("Cart item not found.");
                }
                
                // Ensure the item belongs to the user's cart
                if (cartItem.CartId != cart.Id)
                {
                    return ServiceResult<CartDto>.Failure("Cart item does not belong to your cart");
                }
                
                // Check stock availability if increasing quantity
                if (request.Quantity > cartItem.Quantity)
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(cartItem.ProductId);
                    if (product != null && product.StockQuantity < request.Quantity)
                    {
                        return ServiceResult<CartDto>.Failure($"Not enough stock available. Only {product.StockQuantity} items left.");
                    }
                }
                
                // Update quantity
                await _unitOfWork.Carts.UpdateCartItemQuantityAsync(request.CartItemId, request.Quantity);
                
                // Save changes
                await _unitOfWork.SaveChangesAsync();
                
                // Refresh cart with updated items
                var updatedCart = await _unitOfWork.Carts.GetCartWithItemsByIdAsync(cart.Id);
                
                var cartDto = updatedCart != null 
                    ? updatedCart.ToCartDto() 
                    : new CartDto { Id = cart.Id, CustomerId = cart.CustomerId };
                
                return ServiceResult<CartDto>.Success(cartDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating cart item quantity for user {UserId}", userId);
                return ServiceResult<CartDto>.Failure("Failed to update item quantity. Please try again later.");
            }
        }        public async Task<ServiceResult<CartDto>> RemoveCartItemAsync(string userId, int cartItemId)
        {
            try
            {
                // Find customer from user ID
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    return ServiceResult<CartDto>.Failure("Customer account not found");
                }
                
                // Get the cart
                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customer.Id);
                if (cart == null)
                {
                    return ServiceResult<CartDto>.Failure("Cart not found");
                }
                
                // Get the specific cart item
                var cartItem = await _unitOfWork.Carts.GetCartItemByIdAsync(cartItemId);
                if (cartItem == null)
                {
                    return ServiceResult<CartDto>.Failure("Cart item not found.");
                }
                
                // Ensure the item belongs to the user's cart
                if (cartItem.CartId != cart.Id)
                {
                    return ServiceResult<CartDto>.Failure("Cart item does not belong to your cart");
                }
                
                // Remove item
                await _unitOfWork.Carts.RemoveCartItemAsync(cartItemId);
                
                // Save changes
                await _unitOfWork.SaveChangesAsync();
                
                // Refresh cart with updated items
                var updatedCart = await _unitOfWork.Carts.GetCartWithItemsByIdAsync(cart.Id);
                
                var cartDto = updatedCart != null 
                    ? updatedCart.ToCartDto() 
                    : new CartDto { Id = cart.Id, CustomerId = cart.CustomerId };
                
                return ServiceResult<CartDto>.Success(cartDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cart item for user {UserId}", userId);
                return ServiceResult<CartDto>.Failure("Failed to remove item from cart. Please try again later.");
            }
        }        public async Task<ServiceResult<bool>> ClearCartAsync(string userId)
        {
            try
            {
                // Find customer from user ID
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    return ServiceResult<bool>.Failure("Customer account not found");
                }
                
                // Get the cart
                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customer.Id);
                if (cart == null)
                {
                    return ServiceResult<bool>.Success(true); // No cart to clear, consider it successful
                }
                
                // Clear cart items
                await _unitOfWork.Carts.ClearCartAsync(cart.Id);
                
                // Save changes
                await _unitOfWork.SaveChangesAsync();
                
                return ServiceResult<bool>.Success(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing cart for user {UserId}", userId);
                return ServiceResult<bool>.Failure("Failed to clear cart. Please try again later.");
            }
        }        
        
        public async Task<ServiceResult<CartDto>> IncreaseCartItemQuantityAsync(string userId, int cartItemId)
        {
            try
            {
                // Find customer from user ID
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    return ServiceResult<CartDto>.Failure("Customer account not found");
                }
                
                // Get the cart
                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customer.Id);
                if (cart == null)
                {
                    return ServiceResult<CartDto>.Failure("Cart not found");
                }
                
                // Get the specific cart item
                var cartItem = await _unitOfWork.Carts.GetCartItemByIdAsync(cartItemId);
                if (cartItem == null)
                {
                    return ServiceResult<CartDto>.Failure("Cart item not found.");
                }
                
                // Ensure the item belongs to the user's cart
                if (cartItem.CartId != cart.Id)
                {
                    return ServiceResult<CartDto>.Failure("Cart item does not belong to your cart");
                }
                
                // Increase quantity (this check is now in the repository)
                var increaseResult = await _unitOfWork.Carts.IncreaseCartItemQuantityAsync(cartItemId);
                if (!increaseResult)
                {
                    return ServiceResult<CartDto>.Failure($"Cannot increase quantity. Not enough items available in stock.");
                }
                
                // Save changes
                await _unitOfWork.SaveChangesAsync();
                
                // Refresh cart with updated items
                var updatedCart = await _unitOfWork.Carts.GetCartWithItemsByIdAsync(cart.Id);
                
                var cartDto = updatedCart != null 
                    ? updatedCart.ToCartDto() 
                    : new CartDto { Id = cart.Id, CustomerId = cart.CustomerId };
                
                return ServiceResult<CartDto>.Success(cartDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error increasing cart item quantity for user {UserId}", userId);
                return ServiceResult<CartDto>.Failure("Failed to increase item quantity. Please try again later.");
            }
        }        public async Task<ServiceResult<CartDto>> DecreaseCartItemQuantityAsync(string userId, int cartItemId)
        {
            try
            {
                // Find customer from user ID
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    return ServiceResult<CartDto>.Failure("Customer account not found");
                }
                
                // Get the cart
                var cart = await _unitOfWork.Carts.GetCartWithItemsByCustomerIdAsync(customer.Id);
                if (cart == null)
                {
                    return ServiceResult<CartDto>.Failure("Cart not found");
                }
                
                // Get the specific cart item
                var cartItem = await _unitOfWork.Carts.GetCartItemByIdAsync(cartItemId);
                if (cartItem == null)
                {
                    return ServiceResult<CartDto>.Failure("Cart item not found.");
                }
                
                // Ensure the item belongs to the user's cart
                if (cartItem.CartId != cart.Id)
                {
                    return ServiceResult<CartDto>.Failure("Cart item does not belong to your cart");
                }
                
                // Decrease quantity or remove if only 1 left (this logic is now in the repository)
                await _unitOfWork.Carts.DecreaseCartItemQuantityAsync(cartItemId);
                
                // Save changes
                await _unitOfWork.SaveChangesAsync();
                
                // Refresh cart with updated items
                var updatedCart = await _unitOfWork.Carts.GetCartWithItemsByIdAsync(cart.Id);
                
                var cartDto = updatedCart != null 
                    ? updatedCart.ToCartDto() 
                    : new CartDto { Id = cart.Id, CustomerId = cart.CustomerId };
                
                return ServiceResult<CartDto>.Success(cartDto);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error decreasing cart item quantity for user {UserId}", userId);
                return ServiceResult<CartDto>.Failure("Failed to decrease item quantity. Please try again later.");
            }
        }        public async Task<ServiceResult<int>> GetCartItemCountAsync(string userId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return ServiceResult<int>.Success(0); // No user, no cart
                }
                
                // Find the customer
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    return ServiceResult<int>.Success(0);
                }
                
                // Get cart item count directly from repository
                int itemCount = await _unitOfWork.Carts.GetCartItemCountAsync(customer.Id);
                
                return ServiceResult<int>.Success(itemCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cart item count for user {UserId}", userId);
                return ServiceResult<int>.Failure("Failed to retrieve cart item count.");
            }
        }        public async Task<ServiceResult<bool>> IsProductInCartAsync(string userId, int productId)
        {
            try
            {
                if (string.IsNullOrEmpty(userId))
                {
                    return ServiceResult<bool>.Success(false);
                }
                
                // Find the customer
                var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(userId);
                if (customer == null)
                {
                    return ServiceResult<bool>.Success(false);
                }
                
                // Check directly in repository if the product is in the cart
                bool isInCart = await _unitOfWork.Carts.IsProductInCartAsync(customer.Id, productId);
                return ServiceResult<bool>.Success(isInCart);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if product {ProductId} is in cart for user {UserId}", productId, userId);
                return ServiceResult<bool>.Failure("Failed to check if product is in cart.");
            }
        }
    }
}