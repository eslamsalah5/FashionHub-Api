using Domain.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Repositories.Interfaces
{    public interface IOrderRepository : IGenericRepository<Order>
    {
        Task<IEnumerable<Order>> GetCustomerOrdersAsync(string customerId);
        Task<Order?> GetOrderWithItemsAsync(int orderId);
        Task<Order?> CreateOrderFromCartAsync(int cartId);
    }
}
