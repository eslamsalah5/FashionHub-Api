using Domain.Enums;
using System;

namespace Application.DTOs.Products
{
    public class ProductDto
    {
        public int Id { get; set; }
        
        public string Name { get; set; } = string.Empty;
        
        public string Description { get; set; } = string.Empty;
        
        public decimal Price { get; set; }
        
        public decimal? DiscountPrice { get; set; }
        
        public bool IsOnSale { get; set; } = false;
        
        public int StockQuantity { get; set; }
        
        public string SKU { get; set; } = string.Empty;
        
        public ProductCategory Category { get; set; }
        
        public Gender Gender { get; set; } = Gender.Unisex;
        
        public string AvailableSizes { get; set; } = string.Empty;
        
        public string AvailableColors { get; set; } = string.Empty;
        
        public string Brand { get; set; } = string.Empty;
        
        public string MainImageUrl { get; set; } = string.Empty;
        
        public string AdditionalImageUrls { get; set; } = string.Empty;
        
        public decimal AverageRating { get; set; } = 0;
        
        public int NumberOfRatings { get; set; } = 0;
        
        public string Slug { get; set; } = string.Empty;
        
        public bool IsFeatured { get; set; } = false;
          public bool IsActive { get; set; } = true;
        
        public DateTime DateCreated { get; set; }
        
        public string Tags { get; set; } = string.Empty;
    }
}
