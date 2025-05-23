using Application.DTOs.Products;
using Application.Models;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Domain.Enums;

namespace Application.Services.Interfaces
{
    public interface IProductService
    {
        // Create operations
        Task<ServiceResult<ProductDto>> CreateProductAsync(CreateProductDto createProductDto);
          // Read operations
        Task<ServiceResult<ProductDto>> GetProductByIdAsync(int id);
        Task<ServiceResult<IReadOnlyList<ProductDto>>> GetAllProductsAsync(int pageIndex, int pageSize);
        Task<ServiceResult<IReadOnlyList<ProductDto>>> GetProductsByCategoryAsync(ProductCategory category, int pageIndex, int pageSize);
        Task<ServiceResult<IReadOnlyList<ProductDto>>> GetFeaturedProductsAsync();
        Task<ServiceResult<IReadOnlyList<ProductDto>>> GetProductsOnSaleAsync(int pageIndex, int pageSize);
        Task<ServiceResult<IReadOnlyList<ProductDto>>> SearchProductsAsync(string searchTerm, int pageIndex, int pageSize);
        Task<ServiceResult<int>> GetTotalProductCountAsync();
          // Update operations
        Task<ServiceResult> UpdateProductAsync(int id, UpdateProductDto updateProductDto);
        Task<ServiceResult> UpdateStockQuantityAsync(int id, int quantity);
        Task<ServiceResult> ToggleProductStatusAsync(int id, bool isActive);
        Task<ServiceResult> ToggleFeaturedStatusAsync(int id, bool isFeatured);
        
        // Delete operations
        Task<ServiceResult> DeleteProductAsync(int id);
    }
}
