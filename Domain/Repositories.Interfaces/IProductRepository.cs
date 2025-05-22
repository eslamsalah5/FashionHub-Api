using Domain.Entities;
using Domain.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Repositories.Interfaces
{
    public interface IProductRepository : IGenericRepository<Product>
    {
        Task<IReadOnlyList<Product>> GetProductsByCategoryAsync(ProductCategory category);
        Task<IReadOnlyList<Product>> GetFeaturedProductsAsync();
        Task<IReadOnlyList<Product>> GetProductsOnSaleAsync();
        Task<IReadOnlyList<Product>> GetProductsByBrandAsync(string brand);
        Task<IReadOnlyList<Product>> SearchProductsAsync(string searchTerm);
        Task<bool> UpdateStockQuantityAsync(int productId, int quantity);
    }
}
