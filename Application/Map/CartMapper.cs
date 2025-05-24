using Application.DTOs.Cart;
using Domain.Entities;
using System.Collections.Generic;
using System.Linq;

namespace Application.Map
{
    public static class CartMapper
    {
        public static CartDto ToCartDto(this Cart cart)
        {
            if (cart == null)
            {
                return new CartDto();
            }
            
            return new CartDto
            {
                Id = cart.Id,
                CustomerId = cart.CustomerId,
                CreatedAt = cart.CreatedAt,
                ModifiedAt = cart.ModifiedAt,
                Items = cart.CartItems?.Select(ci => ci.ToCartItemDto()).ToList() ?? new List<CartItemDto>()
            };
        }

        public static CartItemDto ToCartItemDto(this CartItem cartItem)
        {
            if (cartItem == null)
            {
                return new CartItemDto();
            }
            
            return new CartItemDto
            {
                Id = cartItem.Id,
                ProductId = cartItem.ProductId,
                ProductName = cartItem.Product?.Name ?? string.Empty,
                ProductImageUrl = cartItem.Product?.MainImageUrl ?? string.Empty,
                Quantity = cartItem.Quantity,
                UnitPrice = cartItem.PriceAtAddition
            };
        }
    }
}
