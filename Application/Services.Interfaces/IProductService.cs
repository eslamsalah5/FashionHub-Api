using Application.DTOs.Products;
using Application.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using Domain.Enums;

namespace Application.Services.Interfaces
{
    public interface IProductService
    {
        // Create
        Task<ServiceResult<ProductDto>> CreateProductAsync(CreateProductDto createProductDto);

        // Read — all paged methods now return PagedResult (items + metadata bundled)
        Task<ServiceResult<ProductDto>> GetProductByIdAsync(int id);
        Task<ServiceResult<PagedResult<ProductDto>>> GetAllProductsAsync(int pageIndex, int pageSize);
        Task<ServiceResult<PagedResult<ProductDto>>> GetProductsByCategoryAsync(ProductCategory category, int pageIndex, int pageSize);
        Task<ServiceResult<IReadOnlyList<ProductDto>>> GetFeaturedProductsAsync();
        Task<ServiceResult<PagedResult<ProductDto>>> GetProductsOnSaleAsync(int pageIndex, int pageSize);
        Task<ServiceResult<PagedResult<ProductDto>>> SearchProductsAsync(string searchTerm, int pageIndex, int pageSize);

        // Update
        Task<ServiceResult> UpdateProductAsync(int id, UpdateProductDto updateProductDto);
        Task<ServiceResult> UpdateStockQuantityAsync(int id, int quantity);
        Task<ServiceResult> ToggleProductStatusAsync(int id, bool isActive);
        Task<ServiceResult> ToggleFeaturedStatusAsync(int id, bool isFeatured);

        // Delete
        /// <summary>Marks the product as deleted (IsDeleted = true). Images are preserved on disk.</summary>
        Task<ServiceResult> SoftDeleteProductAsync(int id);

        /// <summary>Permanently removes the product from the database AND deletes its images from disk.</summary>
        Task<ServiceResult> HardDeleteProductAsync(int id);
    }
}
