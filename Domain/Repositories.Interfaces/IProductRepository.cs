using Domain.Entities;
using Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Repositories.Interfaces
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        // Paged queries — filtering & pagination happen in SQL, not in memory
        Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(int pageIndex, int pageSize);
        Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedByCategoryAsync(ProductCategory category, int pageIndex, int pageSize);
        Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedOnSaleAsync(int pageIndex, int pageSize);
        Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchPagedAsync(string searchTerm, int pageIndex, int pageSize);

        // Non-paged (small result sets)
        Task<IReadOnlyList<Product>> GetFeaturedProductsAsync();
        Task<IReadOnlyList<Product>> GetProductsByBrandAsync(string brand);

        // Stock
        Task<bool> UpdateStockQuantityAsync(int productId, int quantity);

        // Hard delete — physically removes the record and is safe to call after images are already deleted
        Task HardDeleteAsync(Product product);
    }
}
