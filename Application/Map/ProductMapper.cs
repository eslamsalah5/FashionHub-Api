using Application.DTOs.Products;
using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Application.Map
{
    public static class ProductMapper
    {
        /// <summary>
        /// Maps a Product entity to a ProductDto
        /// </summary>
        /// <param name="product">Product entity to map</param>
        /// <returns>Mapped ProductDto</returns>
        public static ProductDto ToProductDto(this Product product)
        {
            if (product == null)
                return null;            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                DiscountPrice = product.DiscountPrice,
                IsOnSale = product.IsOnSale,
                StockQuantity = product.StockQuantity,
                SKU = product.SKU,
                Category = product.Category,
                Gender = product.Gender,
                AvailableSizes = product.AvailableSizes,
                AvailableColors = product.AvailableColors,
                Brand = product.Brand,
                MainImageUrl = NormalizeImagePath(product.MainImageUrl),
                AdditionalImageUrls = NormalizeAdditionalImages(product.AdditionalImageUrls),
                AverageRating = product.AverageRating,
                NumberOfRatings = product.NumberOfRatings,                Slug = product.Slug,
                IsFeatured = product.IsFeatured,
                IsActive = product.IsActive,
                DateCreated = product.DateCreated,
                Tags = product.Tags
            };
        }        /// <summary>
        /// Maps a CreateProductDto to a Product entity
        /// </summary>
        /// <param name="dto">CreateProductDto to map</param>
        /// <param name="mainImagePath">Path of the uploaded main image</param>
        /// <param name="additionalImagePaths">Paths of the uploaded additional images</param>
        /// <returns>Mapped Product entity</returns>
        public static Product ToEntity(this CreateProductDto dto, string mainImagePath, string additionalImagePaths)
        {
            if (dto == null)
                return null;

            return new Product
            {
                Name = dto.Name,
                Description = dto.Description,
                Price = dto.Price,
                DiscountPrice = dto.DiscountPrice,
                IsOnSale = dto.IsOnSale,
                StockQuantity = dto.StockQuantity,
                Category = dto.Category,
                Gender = dto.Gender,
                AvailableSizes = dto.AvailableSizes,
                AvailableColors = dto.AvailableColors,
                Brand = dto.Brand,
                MainImageUrl = mainImagePath,
                AdditionalImageUrls = additionalImagePaths,
                Tags = dto.Tags,
                Slug = GenerateSlug(dto.Name),
                DateCreated = DateTime.UtcNow,
                IsActive = true
            };
        }        /// <summary>
        /// Updates an existing Product entity with values from UpdateProductDto
        /// </summary>
        /// <param name="product">Product entity to update</param>
        /// <param name="dto">UpdateProductDto with new values</param>
        /// <param name="mainImagePath">Path of the updated main image (if provided)</param>
        /// <param name="additionalImagePaths">Paths of the updated additional images (if provided)</param>
        public static void UpdateEntity(this Product product, UpdateProductDto dto, string? mainImagePath = null, string? additionalImagePaths = null)
        {
            if (product == null || dto == null)
                return;

            product.Name = dto.Name;
            product.Description = dto.Description;
            product.Price = dto.Price;
            product.DiscountPrice = dto.DiscountPrice;
            product.IsOnSale = dto.IsOnSale;
            product.StockQuantity = dto.StockQuantity;
            product.Category = dto.Category;
            product.Gender = dto.Gender;
            product.AvailableSizes = dto.AvailableSizes;
            product.AvailableColors = dto.AvailableColors;            product.Brand = dto.Brand;
            product.Tags = dto.Tags;
            product.IsFeatured = dto.IsFeatured;
            product.IsActive = dto.IsActive;
            product.Slug = GenerateSlug(dto.Name);

            // Update image paths (check for explicit clear flags first)
            bool shouldClearMainImage = !string.IsNullOrEmpty(dto.ClearMainImage) && 
                                       (dto.ClearMainImage.Equals("true", StringComparison.OrdinalIgnoreCase) || 
                                        dto.ClearMainImage.Equals("1"));
            if (shouldClearMainImage)
            {
                product.MainImageUrl = string.Empty;
            }
            else if (mainImagePath != null)
            {
                product.MainImageUrl = mainImagePath;
            }

            bool shouldClearAdditionalImages = !string.IsNullOrEmpty(dto.ClearAdditionalImages) && 
                                             (dto.ClearAdditionalImages.Equals("true", StringComparison.OrdinalIgnoreCase) || 
                                              dto.ClearAdditionalImages.Equals("1"));
            if (shouldClearAdditionalImages)
            {
                product.AdditionalImageUrls = string.Empty;
            }
            else if (additionalImagePaths != null)
            {
                product.AdditionalImageUrls = additionalImagePaths;
            }
        }

        /// <summary>
        /// Map a list of Product entities to ProductDto objects
        /// </summary>
        /// <param name="products">List of Product entities</param>
        /// <returns>List of ProductDto objects</returns>
        public static IReadOnlyList<ProductDto> ToProductDtoList(this IEnumerable<Product> products)
        {
            if (products == null)
                return new List<ProductDto>();

            return products.Select(p => p.ToProductDto()).ToList();
        }

        /// <summary>
        /// Generates a URL-friendly slug from a product name
        /// </summary>
        /// <param name="name">Product name</param>
        /// <returns>URL-friendly slug</returns>
        private static string GenerateSlug(string name)
        {
            // Simple slug generation - in a real app, you'd want to ensure uniqueness
            return name.ToLower()
                .Replace(" ", "-")
                .Replace("&", "and")
                .Replace("'", "")
                .Replace("\"", "")
                .Replace("?", "")
                .Replace("!", "")
                .Replace(":", "")
                .Replace(";", "")
                .Replace("/", "-")
                .Replace("\\", "-");
        }

        public static string NormalizeImagePath(string? path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            if (path.StartsWith("http") || path.StartsWith("/")) return path;
            return $"/uploads/Products/{path}";
        }

        private static string NormalizeAdditionalImages(string? paths)
        {
            if (string.IsNullOrEmpty(paths)) return string.Empty;
            return string.Join(",", paths.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => NormalizeImagePath(p.Trim())));
        }
    }
}
