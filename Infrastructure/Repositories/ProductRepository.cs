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

        // ─────────────────────────────────────────────────────────────────────
        // PAGED QUERIES — all filtering & pagination is translated to SQL
        // ─────────────────────────────────────────────────────────────────────

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedAsync(
            int pageIndex, int pageSize)
        {
            var query = _dbSet
                .Where(p => p.IsActive && !p.IsDeleted)
                .OrderByDescending(p => p.DateCreated);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedByCategoryAsync(
            ProductCategory category, int pageIndex, int pageSize)
        {
            var query = _dbSet
                .Where(p => p.Category == category && p.IsActive && !p.IsDeleted)
                .OrderByDescending(p => p.DateCreated);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> GetPagedOnSaleAsync(
            int pageIndex, int pageSize)
        {
            var query = _dbSet
                .Where(p => p.IsOnSale && p.IsActive && !p.IsDeleted && p.DiscountPrice.HasValue)
                .OrderByDescending(p => p.DateCreated);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<(IReadOnlyList<Product> Items, int TotalCount)> SearchPagedAsync(
            string searchTerm, int pageIndex, int pageSize)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
                return (new List<Product>(), 0);

            var term = searchTerm.ToLower();

            var query = _dbSet
                .Where(p => p.IsActive && !p.IsDeleted &&
                            (p.Name.ToLower().Contains(term) ||
                             p.Description.ToLower().Contains(term) ||
                             p.Brand.ToLower().Contains(term) ||
                             p.Tags.ToLower().Contains(term)))
                .OrderByDescending(p => p.DateCreated);

            var totalCount = await query.CountAsync();
            var items = await query
                .Skip(pageIndex * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        // ─────────────────────────────────────────────────────────────────────
        // NON-PAGED (small, bounded result sets)
        // ─────────────────────────────────────────────────────────────────────

        public async Task<IReadOnlyList<Product>> GetFeaturedProductsAsync()
        {
            return await _dbSet
                .Where(p => p.IsFeatured && p.IsActive && !p.IsDeleted)
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();
        }

        public async Task<IReadOnlyList<Product>> GetProductsByBrandAsync(string brand)
        {
            return await _dbSet
                .Where(p => p.Brand == brand && p.IsActive && !p.IsDeleted)
                .OrderByDescending(p => p.DateCreated)
                .ToListAsync();
        }

        // ─────────────────────────────────────────────────────────────────────
        // STOCK
        // ─────────────────────────────────────────────────────────────────────

        public async Task<bool> UpdateStockQuantityAsync(int productId, int quantity)
        {
            var product = await _dbSet.FindAsync(productId);
            if (product == null)
                return false;

            product.StockQuantity = quantity;
            return true;
        }

        public async Task<Product?> GetByIdIncludingDeletedAsync(int id)
        {
            return await _dbSet
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        // ─────────────────────────────────────────────────────────────────────
        // HARD DELETE — physically removes the row from the database.
        // Caller is responsible for deleting associated image files BEFORE calling this.
        // ─────────────────────────────────────────────────────────────────────

        public Task HardDeleteAsync(Product product)
        {
            _dbSet.Remove(product);
            return Task.CompletedTask;
        }
    }
}
