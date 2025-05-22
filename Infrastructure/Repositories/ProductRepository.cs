using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class ProductRepository : GenericRepository<Product>, IProductRepository
    {
        public ProductRepository(ApplicationDbContext context) : base(context)
        {
        }

        public async Task<IReadOnlyList<Product>> GetProductsByCategoryAsync(ProductCategory category)
        {
            return await _dbSet
                .Where(p => p.Category == category && p.IsActive)
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetFeaturedProductsAsync()
        {
            return await _dbSet
                .Where(p => p.IsFeatured && p.IsActive)
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetProductsOnSaleAsync()
        {
            return await _dbSet
                .Where(p => p.IsOnSale && p.IsActive && p.DiscountPrice.HasValue)
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetProductsByBrandAsync(string brand)
        {
            return await _dbSet
                .Where(p => p.Brand == brand && p.IsActive)
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> SearchProductsAsync(string searchTerm)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return new List<Product>();

            searchTerm = searchTerm.ToLower();

            return await _dbSet
                .Where(p => p.IsActive &&
                           (p.Name.ToLower().Contains(searchTerm) ||
                            p.Description.ToLower().Contains(searchTerm) ||
                            p.Brand.ToLower().Contains(searchTerm) ||
                            p.Tags.ToLower().Contains(searchTerm)))
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();
        }

        public async Task<bool> UpdateStockQuantityAsync(int productId, int quantity)
        {
            var product = await _dbSet.FindAsync(productId);
            
            if (product == null)
                return false;
                
            product.StockQuantity = quantity;
            return true;
        }
    }
}
