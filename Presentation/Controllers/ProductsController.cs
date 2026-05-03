using Application.DTOs.Products;
using Application.Models;
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

        // ─────────────────────────────────────────────────────────────────────
        // READ
        // ─────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> GetProducts([FromQuery] int pageIndex = 0, [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Min(pageSize, 50);
            var result = await _productService.GetAllProductsAsync(pageIndex, pageSize);

            if (result.IsSuccess)
                return Ok(result.Data);

            return MapErrorToResponse(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetProduct(int id)
        {
            var result = await _productService.GetProductByIdAsync(id);

            if (result.IsSuccess)
                return Ok(result.Data);

            return MapErrorToResponse(result);
        }

        [HttpGet("category/{category}")]
        public async Task<IActionResult> GetProductsByCategory(
            ProductCategory category,
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Min(pageSize, 50);
            var result = await _productService.GetProductsByCategoryAsync(category, pageIndex, pageSize);

            if (result.IsSuccess)
                return Ok(result.Data);

            return MapErrorToResponse(result);
        }

        [HttpGet("featured")]
        public async Task<IActionResult> GetFeaturedProducts()
        {
            var result = await _productService.GetFeaturedProductsAsync();

            if (result.IsSuccess)
                return Ok(result.Data);

            return MapErrorToResponse(result);
        }

        [HttpGet("sale")]
        public async Task<IActionResult> GetProductsOnSale(
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Min(pageSize, 50);
            var result = await _productService.GetProductsOnSaleAsync(pageIndex, pageSize);

            if (result.IsSuccess)
                return Ok(result.Data);

            return MapErrorToResponse(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] string term,
            [FromQuery] int pageIndex = 0,
            [FromQuery] int pageSize = 10)
        {
            pageSize = Math.Min(pageSize, 50);
            var result = await _productService.SearchProductsAsync(term, pageIndex, pageSize);

            if (result.IsSuccess)
                return Ok(result.Data);

            return MapErrorToResponse(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // CREATE
        // ─────────────────────────────────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        [HttpPost]
        public async Task<IActionResult> CreateProduct([FromForm] CreateProductDto createProductDto)
        {
            var result = await _productService.CreateProductAsync(createProductDto);

            if (result.IsSuccess && result.Data != null)
                return CreatedAtAction(nameof(GetProduct), new { id = result.Data.Id }, result.Data);

            return MapErrorToResponse(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // UPDATE
        // ─────────────────────────────────────────────────────────────────────

        [Authorize(Roles = "Admin")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProduct(int id, [FromForm] UpdateProductDto updateProductDto)
        {
            var result = await _productService.UpdateProductAsync(id, updateProductDto);

            if (result.IsSuccess)
                return NoContent();

            return MapErrorToResponse(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/stock")]
        public async Task<IActionResult> UpdateStockQuantity(int id, [FromBody] int quantity)
        {
            var result = await _productService.UpdateStockQuantityAsync(id, quantity);

            if (result.IsSuccess)
                return NoContent();

            return MapErrorToResponse(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> ToggleProductStatus(int id, [FromBody] bool isActive)
        {
            var result = await _productService.ToggleProductStatusAsync(id, isActive);

            if (result.IsSuccess)
                return NoContent();

            return MapErrorToResponse(result);
        }

        [Authorize(Roles = "Admin")]
        [HttpPatch("{id}/featured")]
        public async Task<IActionResult> ToggleFeaturedStatus(int id, [FromBody] bool isFeatured)
        {
            var result = await _productService.ToggleFeaturedStatusAsync(id, isFeatured);

            if (result.IsSuccess)
                return NoContent();

            return MapErrorToResponse(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // DELETE
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Soft Delete: marks the product as deleted. Images stay on disk.
        /// The product can be restored later by toggling its status.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}/soft")]
        public async Task<IActionResult> SoftDeleteProduct(int id)
        {
            var result = await _productService.SoftDeleteProductAsync(id);

            if (result.IsSuccess)
                return NoContent();

            return MapErrorToResponse(result);
        }

        /// <summary>
        /// Hard Delete: permanently removes the product from the database AND deletes its images from disk.
        /// This operation is IRREVERSIBLE.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id}/hard")]
        public async Task<IActionResult> HardDeleteProduct(int id)
        {
            var result = await _productService.HardDeleteProductAsync(id);

            if (result.IsSuccess)
                return NoContent();

            return MapErrorToResponse(result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // HELPER — maps ServiceErrorType to the correct HTTP status code
        // ─────────────────────────────────────────────────────────────────────

        private IActionResult MapErrorToResponse(ServiceResult result)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound    => NotFound(result.Errors),
                ServiceErrorType.Validation  => UnprocessableEntity(result.Errors),
                ServiceErrorType.Conflict    => Conflict(result.Errors),
                ServiceErrorType.Unauthorized => Unauthorized(result.Errors),
                _                            => BadRequest(result.Errors)
            };
        }

        private IActionResult MapErrorToResponse<T>(ServiceResult<T> result)
        {
            return result.ErrorType switch
            {
                ServiceErrorType.NotFound    => NotFound(result.Errors),
                ServiceErrorType.Validation  => UnprocessableEntity(result.Errors),
                ServiceErrorType.Conflict    => Conflict(result.Errors),
                ServiceErrorType.Unauthorized => Unauthorized(result.Errors),
                _                            => BadRequest(result.Errors)
            };
        }
    }
}
