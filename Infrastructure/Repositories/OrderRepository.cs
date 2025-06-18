using Domain.Entities;
using Domain.Repositories.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class OrderRepository : GenericRepository<Order>, IOrderRepository
    {
        public OrderRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IEnumerable<Order>> GetCustomerOrdersAsync(string customerId)
        {
            return await _dbSet
                .Include(o => o.Customer)
                    .ThenInclude(c => c.AppUser)
                .Include(o => o.OrderItems)
                .Where(o => o.CustomerId == customerId && !o.IsDeleted)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();
        }

        public async Task<Order?> GetOrderWithItemsAsync(int orderId)
        {
            return await _dbSet
                .Include(o => o.Customer)
                    .ThenInclude(c => c.AppUser)
                .Include(o => o.OrderItems)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId && !o.IsDeleted);
        }

        public async Task<Order?> CreateOrderFromCartAsync(int cartId)
        {
            // Get the cart with items
            var cart = await _context.Carts
                .Include(c => c.CartItems)
                .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(c => c.Id == cartId && !c.IsDeleted);

            if (cart == null) return null;

            // Create a new order
            var order = new Order
            {
                CustomerId = cart.CustomerId,
                TotalAmount = cart.CartItems.Sum(ci => ci.Quantity * (ci.Product.IsOnSale && ci.Product.DiscountPrice.HasValue 
                    ? ci.Product.DiscountPrice.Value 
                    : ci.Product.Price))
            };

            // Add order items
            foreach (var cartItem in cart.CartItems)
            {
                var product = cartItem.Product;
                var price = product.IsOnSale && product.DiscountPrice.HasValue 
                    ? product.DiscountPrice.Value 
                    : product.Price;

                order.OrderItems.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    ProductSKU = product.SKU,
                    UnitPrice = price,
                    Quantity = cartItem.Quantity,
                    Subtotal = price * cartItem.Quantity
                });

                // Update product stock
                product.StockQuantity -= cartItem.Quantity;
            }

            // Add the new order to context
            await _context.Orders.AddAsync(order);

            // Clear the cart items (optional, depending on business logic)
            _context.Set<CartItem>().RemoveRange(cart.CartItems);

            return order;
        }
    }
}
