using Application.DTOs.Products;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Domain.Enums;

namespace Presentation.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _productService.GetAllProductsAsync(pageIndex, pageSize);
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return BadRequest(result.Errors);
        }
        
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var result = await _productService.GetProductByIdAsync(id);
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return NotFound(result.Errors);
        }
        
        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetProductsByCategory(ProductCategory category, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _productService.GetProductsByCategoryAsync(category, pageIndex, pageSize);
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return BadRequest(result.Errors);
        }
        
        [HttpGet("featured")]
        public async Task<IActionResult> GetFeaturedProducts()
        {
            var result = await _productService.GetFeaturedProductsAsync();
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return BadRequest(result.Errors);
        }
        
        [HttpGet("sale")]
        public async Task<IActionResult> GetProductsOnSale([FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _productService.GetProductsOnSaleAsync(pageIndex, pageSize);
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return BadRequest(result.Errors);
        }
        
        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts([FromQuery] string term, [FromQuery] int pageIndex = 1, [FromQuery] int pageSize = 10)
        {
            var result = await _productService.SearchProductsAsync(term, pageIndex, pageSize);
            
            if (result.IsSuccess)
                return Ok(result.Data);
                
            return BadRequest(result.Errors);
        }
          [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromForm] CreateProductDto createProductDto)
        {
            var result = await _productService.CreateProductAsync(createProductDto);
            
            if (result.IsSuccess)
                return CreatedAtAction(nameof(GetProduct), new { id = result.Data.Id }, result.Data);
                
            return BadRequest(result.Errors);
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] UpdateProductDto updateProductDto)
        {
            var result = await _productService.UpdateProductAsync(id, updateProductDto);
            
            if (result.IsSuccess)
                return NoContent();
                
            return result.Errors.Contains("Product not found") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }
          [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/stock")]
        public async Task<IActionResult> UpdateStockQuantity(int id, [FromBody] int quantity)
        {
            var result = await _productService.UpdateStockQuantityAsync(id, quantity);
            
            if (result.IsSuccess)
                return NoContent();
                
            return result.Errors.Contains("Product not found") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ToggleProductStatus(int id, [FromBody] bool isActive)
        {
            var result = await _productService.ToggleProductStatusAsync(id, isActive);
            
            if (result.IsSuccess)
                return NoContent();
                
            return result.Errors.Contains("Product not found") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }
        
        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/featured")]
        public async Task<IActionResult> ToggleFeaturedStatus(int id, [FromBody] bool isFeatured)
        {
            var result = await _productService.ToggleFeaturedStatusAsync(id, isFeatured);
            
            if (result.IsSuccess)
                return NoContent();
                
            return result.Errors.Contains("Product not found") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }
        
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            var result = await _productService.DeleteProductAsync(id);
            
            if (result.IsSuccess)
                return NoContent();
                
            return result.Errors.Contains("Product not found") ? NotFound(result.Errors) : BadRequest(result.Errors);
        }
    }
}
