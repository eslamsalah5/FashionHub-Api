using Domain.Enums;
using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.Products
{
    public class UpdateProductDto
    {
        [Required]
        [StringLength(255)]
        public string Name { get; set; } = string.Empty;
        
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;
        
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0")]
        public decimal Price { get; set; }
        
        [Range(0.01, double.MaxValue, ErrorMessage = "Discount price must be greater than 0")]
        public decimal? DiscountPrice { get; set; }
        
        public bool IsOnSale { get; set; }
        
        [Range(0, int.MaxValue, ErrorMessage = "Stock quantity cannot be negative")]
        public int StockQuantity { get; set; }
        
        [Required]
        public ProductCategory Category { get; set; }
        
        public Gender Gender { get; set; }
        
        [StringLength(255)]
        public string AvailableSizes { get; set; } = string.Empty;
        
        [StringLength(255)]
        public string AvailableColors { get; set; } = string.Empty;
        
        [StringLength(100)]
        public string Brand { get; set; } = string.Empty;
        
        // Optional image updates - null means no change
        public IFormFile? MainImage { get; set; }
        
        public IFormFile[]? AdditionalImages { get; set; }
        
        // Flag to clear existing additional images
        public bool ClearAdditionalImages { get; set; } = false;
        
        public bool IsFeatured { get; set; }
        
        public bool IsActive { get; set; }
        
        public string Tags { get; set; } = string.Empty;
    }
}
