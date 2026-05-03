using Application.DTOs.Products;
using Application.Map;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Application.Services
{
    public class ProductService : IProductService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileService _fileService;
        private readonly IMemoryCache _cache;

        // Cache durations
        private static readonly TimeSpan ProductCacheExpiry = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan FeaturedCacheExpiry = TimeSpan.FromMinutes(15);

        public ProductService(IUnitOfWork unitOfWork, IFileService fileService, IMemoryCache cache)
        {
            _unitOfWork = unitOfWork;
            _fileService = fileService;
            _cache = cache;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Cache helpers
        // Individual product cache keys, so we invalidate only what changed.
        // ─────────────────────────────────────────────────────────────────────

        private static string ProductKey(int id) => $"product_{id}";
        private static string PagedAllKey(int pi, int ps) => $"products_all_{pi}_{ps}";
        private static string PagedCatKey(ProductCategory cat, int pi, int ps) => $"products_cat_{cat}_{pi}_{ps}";
        private static string PagedSaleKey(int pi, int ps) => $"products_sale_{pi}_{ps}";
        private static string SearchKey(string term, int pi, int ps) => $"products_search_{term}_{pi}_{ps}";
        private const string FeaturedKey = "products_featured";

        /// <summary>
        /// Removes only the cache entries that are directly related to the changed product.
        /// List/page caches that might contain this product are cleared by a shared version token.
        /// </summary>
        private void InvalidateProductCache(int productId)
        {
            // Remove individual product entry
            _cache.Remove(ProductKey(productId));

            // Bump version token — all list caches embed this token in their key,
            // so they are effectively expired without needing to enumerate every key.
            _cache.Set("products_list_version", Guid.NewGuid().ToString(), TimeSpan.FromDays(1));

            // Featured is also a list — clear it explicitly since it's a single key
            _cache.Remove(FeaturedKey);
        }

        private string ListVersion()
        {
            return _cache.GetOrCreate("products_list_version",
                entry =>
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(1);
                    return Guid.NewGuid().ToString();
                })!;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────────────

        public async Task<ServiceResult<ProductDto>> CreateProductAsync(CreateProductDto createProductDto)
        {
            try
            {
                string mainImagePath = await _fileService.SaveFileAsync(createProductDto.MainImage, "Products");

                string additionalImagePaths = string.Empty;
                if (createProductDto.AdditionalImages != null && createProductDto.AdditionalImages.Length > 0)
                {
                    var paths = new List<string>();
                    foreach (var image in createProductDto.AdditionalImages)
                    {
                        paths.Add(await _fileService.SaveFileAsync(image, "Products"));
                    }
                    additionalImagePaths = string.Join(",", paths);
                }

                var product = createProductDto.ToEntity(mainImagePath, additionalImagePaths);

                await _unitOfWork.Products.AddAsync(product);
                await _unitOfWork.SaveChangesAsync();

                // New product only affects list caches
                _cache.Set("products_list_version", Guid.NewGuid().ToString(), TimeSpan.FromDays(1));
                _cache.Remove(FeaturedKey);

                return ServiceResult<ProductDto>.Success(product.ToProductDto());
            }
            catch (Exception ex)
            {
                return ServiceResult<ProductDto>.Failure($"Failed to create product: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // READ
        // ─────────────────────────────────────────────────────────────────────

        public async Task<ServiceResult<ProductDto>> GetProductByIdAsync(int id)
        {
            try
            {
                var cacheKey = ProductKey(id);
                if (!_cache.TryGetValue(cacheKey, out ProductDto? productDto))
                {
                    var product = await _unitOfWork.Products.GetByIdAsync(id);
                    if (product == null || product.IsDeleted)
                        return ServiceResult<ProductDto>.NotFound("Product not found");

                    productDto = product.ToProductDto();
                    _cache.Set(cacheKey, productDto, ProductCacheExpiry);
                }

                return ServiceResult<ProductDto>.Success(productDto!);
            }
            catch (Exception ex)
            {
                return ServiceResult<ProductDto>.Failure($"Failed to get product: {ex.Message}");
            }
        }

        public async Task<ServiceResult<PagedResult<ProductDto>>> GetAllProductsAsync(int pageIndex, int pageSize)
        {
            try
            {
                var cacheKey = $"{PagedAllKey(pageIndex, pageSize)}_{ListVersion()}";
                if (!_cache.TryGetValue(cacheKey, out PagedResult<ProductDto>? result))
                {
                    var (items, totalCount) = await _unitOfWork.Products.GetPagedAsync(pageIndex, pageSize);
                    result = new PagedResult<ProductDto>(items.ToProductDtoList(), pageIndex, pageSize, totalCount);
                    _cache.Set(cacheKey, result, ProductCacheExpiry);
                }

                return ServiceResult<PagedResult<ProductDto>>.Success(result!);
            }
            catch (Exception ex)
            {
                return ServiceResult<PagedResult<ProductDto>>.Failure($"Failed to get products: {ex.Message}");
            }
        }

        public async Task<ServiceResult<PagedResult<ProductDto>>> GetProductsByCategoryAsync(
            ProductCategory category, int pageIndex, int pageSize)
        {
            try
            {
                var cacheKey = $"{PagedCatKey(category, pageIndex, pageSize)}_{ListVersion()}";
                if (!_cache.TryGetValue(cacheKey, out PagedResult<ProductDto>? result))
                {
                    var (items, totalCount) = await _unitOfWork.Products.GetPagedByCategoryAsync(category, pageIndex, pageSize);
                    result = new PagedResult<ProductDto>(items.ToProductDtoList(), pageIndex, pageSize, totalCount);
                    _cache.Set(cacheKey, result, ProductCacheExpiry);
                }

                return ServiceResult<PagedResult<ProductDto>>.Success(result!);
            }
            catch (Exception ex)
            {
                return ServiceResult<PagedResult<ProductDto>>.Failure($"Failed to get products by category: {ex.Message}");
            }
        }

        public async Task<ServiceResult<IReadOnlyList<ProductDto>>> GetFeaturedProductsAsync()
        {
            try
            {
                if (!_cache.TryGetValue(FeaturedKey, out IReadOnlyList<ProductDto>? cachedProducts))
                {
                    var products = await _unitOfWork.Products.GetFeaturedProductsAsync();
                    cachedProducts = products.ToProductDtoList();
                    _cache.Set(FeaturedKey, cachedProducts, FeaturedCacheExpiry);
                }

                return ServiceResult<IReadOnlyList<ProductDto>>.Success(cachedProducts!);
            }
            catch (Exception ex)
            {
                return ServiceResult<IReadOnlyList<ProductDto>>.Failure($"Failed to get featured products: {ex.Message}");
            }
        }

        public async Task<ServiceResult<PagedResult<ProductDto>>> GetProductsOnSaleAsync(int pageIndex, int pageSize)
        {
            try
            {
                var cacheKey = $"{PagedSaleKey(pageIndex, pageSize)}_{ListVersion()}";
                if (!_cache.TryGetValue(cacheKey, out PagedResult<ProductDto>? result))
                {
                    var (items, totalCount) = await _unitOfWork.Products.GetPagedOnSaleAsync(pageIndex, pageSize);
                    result = new PagedResult<ProductDto>(items.ToProductDtoList(), pageIndex, pageSize, totalCount);
                    _cache.Set(cacheKey, result, ProductCacheExpiry);
                }

                return ServiceResult<PagedResult<ProductDto>>.Success(result!);
            }
            catch (Exception ex)
            {
                return ServiceResult<PagedResult<ProductDto>>.Failure($"Failed to get products on sale: {ex.Message}");
            }
        }

        public async Task<ServiceResult<PagedResult<ProductDto>>> SearchProductsAsync(
            string searchTerm, int pageIndex, int pageSize)
        {
            try
            {
                var cacheKey = $"{SearchKey(searchTerm, pageIndex, pageSize)}_{ListVersion()}";
                if (!_cache.TryGetValue(cacheKey, out PagedResult<ProductDto>? result))
                {
                    var (items, totalCount) = await _unitOfWork.Products.SearchPagedAsync(searchTerm, pageIndex, pageSize);
                    result = new PagedResult<ProductDto>(items.ToProductDtoList(), pageIndex, pageSize, totalCount);
                    _cache.Set(cacheKey, result, ProductCacheExpiry);
                }

                return ServiceResult<PagedResult<ProductDto>>.Success(result!);
            }
            catch (Exception ex)
            {
                return ServiceResult<PagedResult<ProductDto>>.Failure($"Failed to search products: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────────────────────────────────

        public async Task<ServiceResult> UpdateProductAsync(int id, UpdateProductDto updateProductDto)
        {
            try
            {
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                    return ServiceResult.NotFound("Product not found");

                string? mainImagePath = null;
                if (updateProductDto.MainImage != null)
                {
                    if (!string.IsNullOrEmpty(product.MainImageUrl))
                        _fileService.DeleteFile(product.MainImageUrl);

                    mainImagePath = await _fileService.SaveFileAsync(updateProductDto.MainImage, "Products");
                }

                string? additionalImagePaths = null;
                if (updateProductDto.ClearAdditionalImages)
                {
                    if (!string.IsNullOrEmpty(product.AdditionalImageUrls))
                    {
                        foreach (var imagePath in product.AdditionalImageUrls.Split(','))
                            _fileService.DeleteFile(imagePath.Trim());
                    }
                    additionalImagePaths = string.Empty;
                }
                else if (updateProductDto.AdditionalImages != null && updateProductDto.AdditionalImages.Length > 0)
                {
                    var paths = new List<string>();
                    foreach (var image in updateProductDto.AdditionalImages)
                        paths.Add(await _fileService.SaveFileAsync(image, "Products"));

                    additionalImagePaths = string.Join(",", paths);
                }

                product.UpdateEntity(updateProductDto, mainImagePath, additionalImagePaths);
                await _unitOfWork.SaveChangesAsync();

                InvalidateProductCache(id);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to update product: {ex.Message}");
            }
        }

        public async Task<ServiceResult> UpdateStockQuantityAsync(int id, int quantity)
        {
            try
            {
                var success = await _unitOfWork.Products.UpdateStockQuantityAsync(id, quantity);
                if (!success)
                    return ServiceResult.NotFound("Product not found");

                await _unitOfWork.SaveChangesAsync();

                // Only invalidate the specific product cache — list caches show stock as a field,
                // so we bump the version token too.
                InvalidateProductCache(id);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to update stock: {ex.Message}");
            }
        }

        public async Task<ServiceResult> ToggleProductStatusAsync(int id, bool isActive)
        {
            try
            {
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                    return ServiceResult.NotFound("Product not found");

                product.IsActive = isActive;
                await _unitOfWork.SaveChangesAsync();
                InvalidateProductCache(id);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to toggle product status: {ex.Message}");
            }
        }

        public async Task<ServiceResult> ToggleFeaturedStatusAsync(int id, bool isFeatured)
        {
            try
            {
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                    return ServiceResult.NotFound("Product not found");

                product.IsFeatured = isFeatured;
                await _unitOfWork.SaveChangesAsync();
                InvalidateProductCache(id);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to toggle featured status: {ex.Message}");
            }
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Soft Delete: marks the product as deleted in the database.
        /// Images are intentionally LEFT on disk so the product can be restored later.
        /// </summary>
        public async Task<ServiceResult> SoftDeleteProductAsync(int id)
        {
            try
            {
                var product = await _unitOfWork.Products.GetByIdAsync(id);
                if (product == null || product.IsDeleted)
                    return ServiceResult.NotFound("Product not found");

                _unitOfWork.Products.SoftDelete(product);
                await _unitOfWork.SaveChangesAsync();

                InvalidateProductCache(id);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to soft-delete product: {ex.Message}");
            }
        }

        /// <summary>
        /// Hard Delete: deletes all image files from disk FIRST, then permanently
        /// removes the product row from the database.
        /// This operation is irreversible.
        /// </summary>
        public async Task<ServiceResult> HardDeleteProductAsync(int id)
        {
            try
            {
                // We bypass the soft-delete filter so we can hard-delete even already soft-deleted products.
                var product = await _unitOfWork.Products.GetByIdIncludingDeletedAsync(id);
                if (product == null)
                    return ServiceResult.NotFound("Product not found");

                // 1. Delete images from disk BEFORE removing the DB row
                if (!string.IsNullOrEmpty(product.MainImageUrl))
                    _fileService.DeleteFile(product.MainImageUrl);

                if (!string.IsNullOrEmpty(product.AdditionalImageUrls))
                {
                    foreach (var imagePath in product.AdditionalImageUrls.Split(','))
                        _fileService.DeleteFile(imagePath.Trim());
                }

                // 2. Permanently remove from database
                await _unitOfWork.Products.HardDeleteAsync(product);
                await _unitOfWork.SaveChangesAsync();

                // 3. Remove from cache
                InvalidateProductCache(id);

                return ServiceResult.Success();
            }
            catch (Exception ex)
            {
                return ServiceResult.Failure($"Failed to hard-delete product: {ex.Message}");
            }
        }
    }
}
