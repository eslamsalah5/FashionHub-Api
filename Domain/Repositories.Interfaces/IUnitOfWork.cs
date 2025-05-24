// Domain.Repositories.Interfaces/IUnitOfWork.cs
using Domain.Entities;
using System.Threading.Tasks;

namespace Domain.Repositories.Interfaces
{
    public interface IUnitOfWork
    {   
        // Custom Repositories
        IUserRepository Users { get; }
        IProductRepository Products { get; }
        ICartRepository Carts { get; }
        IOrderRepository Orders { get; }
        IGenericRepository<Admin> Admins { get; }
        IGenericRepository<Customer> Customers { get; }

        Task<int> SaveChangesAsync();
    }
}