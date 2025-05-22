using Application.DTOs.Products;
using Application.Map;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileService _fileService;

        public ProductService(IUnitOfWork unitOfWork, IFileService fileService)
        {
            _unitOfWork = unitOfWork;
            _fileService = fileService;
        }        public async Task<ServiceResult<ProductDto>> CreateProductAsync(CreateProductDto createProductDto)
        {
            // Authentication/Authorization is handled by [Authorize(Roles = "Admin")] at controller level
            try
            {
                // Save main image
                string mainImagePath = await _fileService.SaveFileAsync(createProductDto.MainImage, "Products");

                // Save additional images if any
                string additionalImagePaths = string.Empty;
                if (createProductDto.AdditionalImages != null && createProductDto.AdditionalImages.Length > 0)
                {
                    var paths = new List<string>();
                    foreach (var image in createProductDto.AdditionalImages)
                    {
                        string path = await _fileService.SaveFileAsync(image, "Products");
                        paths.Add(path);
                    }
                    additionalImagePaths = string.Join(",", paths);
                }

                // Create product entity using the mapper extension method
                var product = createProductDto.ToEntity(mainImagePath, additionalImagePaths);

                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveChangesAsync();

                return ServiceResult<ProductDto>.Success(product.ToProductDto());
            }
            catch (Exception ex)
            {
                return ServiceResult<ProductDto>.Failure($"Failed to create product: {ex.Message}");
            }
        }        public async Task<ServiceResult<ProductDto>> GetProductByIdAsync(int id)
        {
            try
            {
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                {
                    return ServiceResult<ProductDto>.Failure("Product not found");
                }

                return ServiceResult<ProductDto>.Success(product.ToProductDto());
            }
            catch (Exception ex)
            {
                return ServiceResult<ProductDto>.Failure($"Failed to get product: {ex.Message}");
            }
        }        public async Task<ServiceResult<IReadOnlyList<ProductDto>>> GetAllProductsAsync(int pageIndex, int pageSize)
        {
            try
            {
                // TODO: Implement proper pagination
                var products = await _unitOfWork.Products.GetAllAsync();
                var productList = products.Where(p => p.IsActive).ToList();

                return ServiceResult<IReadOnlyList<ProductDto>>.Success(productList.ToProductDtoList());
            }
            catch (Exception ex)
            {
                return ServiceResult<IReadOnlyList<ProductDto>>.Failure($"Failed to get products: {ex.Message}");
            }
        }        public async Task<ServiceResult<IReadOnlyList<ProductDto>>> GetProductsByCategoryAsync(ProductCategory category, int pageIndex, int pageSize)
        {
            try
            {
                var products = await _unitOfWork.Products.GetProductsByCategoryAsync(category);
                return ServiceResult<IReadOnlyList<ProductDto>>.Success(products.ToProductDtoList());
            }
            catch (Exception ex)
            {
                return ServiceResult<IReadOnlyList<ProductDto>>.Failure($"Failed to get products: {ex.Message}");
            }
        }        public async Task<ServiceResult<IReadOnlyList<ProductDto>>> GetFeaturedProductsAsync()
        {
            try
            {
                var products = await _unitOfWork.Products.GetFeaturedProductsAsync();
                return ServiceResult<IReadOnlyList<ProductDto>>.Success(products.ToProductDtoList());
            }
            catch (Exception ex)
            {
                return ServiceResult<IReadOnlyList<ProductDto>>.Failure($"Failed to get featured products: {ex.Message}");
            }
        }        public async Task<ServiceResult<IReadOnlyList<ProductDto>>> GetProductsOnSaleAsync(int pageIndex, int pageSize)
        {
            try
            {
                var products = await _unitOfWork.Products.GetProductsOnSaleAsync();
                return ServiceResult<IReadOnlyList<ProductDto>>.Success(products.ToProductDtoList());
            }
            catch (Exception ex)
            {
                return ServiceResult<IReadOnlyList<ProductDto>>.Failure($"Failed to get products on sale: {ex.Message}");
            }
        }        public async Task<ServiceResult<IReadOnlyList<ProductDto>>> SearchProductsAsync(string searchTerm, int pageIndex, int pageSize)
        {
            try
            {
                var products = await _unitOfWork.Products.SearchProductsAsync(searchTerm);
                return ServiceResult<IReadOnlyList<ProductDto>>.Success(products.ToProductDtoList());
            }
            catch (Exception ex)
            {
                return ServiceResult<IReadOnlyList<ProductDto>>.Failure($"Failed to search products: {ex.Message}");
            }
        }          public async Task<ServiceResult> UpdateProductAsync(int id, UpdateProductDto updateProductDto)
        {
            // Authentication/Authorization is handled by [Authorize(Roles = "Admin")] at controller level
            try
            {
                // Get the product
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                {
                    return ServiceResult.Failure("Product not found");
                }

                // Handle image updates if provided
                string mainImagePath = null;
                if (updateProductDto.MainImage != null)
                {
                    // Delete old image if it exists
                    if (!string.IsNullOrEmpty(product.MainImageUrl))
                    {
                        _fileService.DeleteFile(product.MainImageUrl);
                    }

                    // Save new image
                    mainImagePath = await _fileService.SaveFileAsync(updateProductDto.MainImage, "Products");
                }

                // Handle additional images
                string additionalImagePaths = null;
                if (updateProductDto.ClearAdditionalImages)
                {
                    // Delete old additional images
                    if (!string.IsNullOrEmpty(product.AdditionalImageUrls))
                    {
                        foreach (var imagePath in product.AdditionalImageUrls.Split(','))
                        {
                            _fileService.DeleteFile(imagePath.Trim());
                        }
                    }
                    additionalImagePaths = string.Empty;
                }
                else if (updateProductDto.AdditionalImages != null && updateProductDto.AdditionalImages.Length > 0)
                {
                    var paths = new List<string>();
                    foreach (var image in updateProductDto.AdditionalImages)
                    {
                        string path = await _fileService.SaveFileAsync(image, "Products");
                        paths.Add(path);
                    }
                    additionalImagePaths = string.Join(",", paths);
                }                // Update the product using mapper extension method
                product.UpdateEntity(updateProductDto, mainImagePath, additionalImagePaths);

                // Save changes
                await _unitOfWork.SaveChangesAsync();
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to update product: {ex.Message}");
            }
        }        public async Task<ServiceResult> UpdateStockQuantityAsync(int id, int quantity)
        {
            try
            {
                var success = await _unitOfWork.Products.UpdateStockQuantityAsync(id, quantity);
                if (!success)
                {
                    return ServiceResult.Failure("Product not found");
                }
                
                await _unitOfWork.SaveChangesAsync();
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to update stock: {ex.Message}");
            }
        }        public async Task<ServiceResult> ToggleProductStatusAsync(int id, bool isActive)
        {
            try
            {
                // Get the product
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                {
                    return ServiceResult.Failure("Product not found");
                }                // Update product status
                product.IsActive = isActive;

                // Save changes
                await _unitOfWork.SaveChangesAsync();
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to toggle product status: {ex.Message}");
            }
        }        public async Task<ServiceResult> ToggleFeaturedStatusAsync(int id, bool isFeatured)
        {
            try
            {
                // Get the product
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                {
                    return ServiceResult.Failure("Product not found");
                }                // Update product status
                product.IsFeatured = isFeatured;

                // Save changes
                await _unitOfWork.SaveChangesAsync();
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to toggle featured status: {ex.Message}");
            }
        }

        public async Task<ServiceResult> DeleteProductAsync(int id)
        {
            try
            {
                // Get the product
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                {
                    return ServiceResult.Failure("Product not found");
                }

                // Delete images first
                if (!string.IsNullOrEmpty(product.MainImageUrl))
                {
                    _fileService.DeleteFile(product.MainImageUrl);
                }

                if (!string.IsNullOrEmpty(product.AdditionalImageUrls))
                {
                    foreach (var imagePath in product.AdditionalImageUrls.Split(','))
                    {
                        _fileService.DeleteFile(imagePath.Trim());
                    }
                }                // Delete the product (soft delete)
                _unitOfWork.Products.SoftDelete(product);
                await _unitOfWork.SaveChangesAsync();
                
                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to delete product: {ex.Message}");
            }
        }

    }
}
