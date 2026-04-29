using Application.DTOs.Cart;
using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Property-based tests for CartService.AddToCartAsync — Property P8
/// **Validates: Requirements 2.3, 7.2**
/// </summary>
public class CartServiceP8Tests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Custom Arbitrary: generates (stockQuantity, requestQuantity) pairs where
    // requestQuantity > stockQuantity, i.e. the request exceeds available stock.
    // stockQuantity: 1–100
    // requestQuantity: stockQuantity+1 to stockQuantity+200
    // ─────────────────────────────────────────────────────────────────────────

    public class OverstockRequestArbitrary
    {
        /// <summary>
        /// Returns an Arbitrary for (stockQuantity, requestQuantity) tuples
        /// where requestQuantity strictly exceeds stockQuantity.
        /// </summary>
        public static Arbitrary<(int StockQuantity, int RequestQuantity)> OverstockPairs()
        {
            var gen =
                from stock in Gen.Choose(1, 100)
                from excess in Gen.Choose(1, 200)
                select (StockQuantity: stock, RequestQuantity: stock + excess);

            return gen.ToArbitrary();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Property P8 — Stock Validation Before Cart Addition
    // **Validates: Requirements 2.3, 7.2**
    //
    // For any AddToCartAsync call where request.Quantity > product.StockQuantity:
    //   - The service returns a failure result
    //   - AddItemToCartAsync is never called (cart remains unchanged)
    // ─────────────────────────────────────────────────────────────────────────

    [Property(Arbitrary = new[] { typeof(OverstockRequestArbitrary) })]
    public bool P8_StockValidationBeforeCartAddition_RejectsOverstockRequest(
        (int StockQuantity, int RequestQuantity) input)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string userId = "user-p8-test";
        const string customerId = "customer-p8-test";
        const int productId = 42;
        const int cartId = 1;

        var (stockQuantity, requestQuantity) = input;

        // Product with the generated stock quantity
        var product = new DomainProduct
        {
            Id = productId,
            Name = "Test Product",
            SKU = "SKU-P8",
            Price = 29.99m,
            StockQuantity = stockQuantity
        };

        // Customer returned by user lookup
        var customer = new Customer
        {
            Id = customerId
        };

        // Empty cart returned by GetOrCreateCartAsync
        var cart = new Cart
        {
            Id = cartId,
            CustomerId = customerId,
            CartItems = new List<CartItem>()
        };

        // Track whether AddItemToCartAsync was called
        bool addItemWasCalled = false;

        // Mock IProductRepository — returns the product with limited stock
        var mockProductRepo = new Mock<IProductRepository>();
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        // Mock IUserRepository — returns the customer
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync(customer);

        // Mock ICartRepository — returns an empty cart; tracks AddItemToCartAsync calls
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(customerId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.AddItemToCartAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback(() => addItemWasCalled = true)
            .ReturnsAsync(true);

        // Mock IUnitOfWork
        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Mock ILogger<CartService>
        var mockLogger = new Mock<ILogger<CartService>>();

        var service = new CartService(mockUow.Object, mockLogger.Object);

        var request = new AddToCartDto
        {
            ProductId = productId,
            Quantity = requestQuantity,
            SelectedSize = "M",
            SelectedColor = "Black"
        };

        // ── Act ───────────────────────────────────────────────────────────────
        var result = service.AddToCartAsync(userId, request).GetAwaiter().GetResult();

        // ── Assert ────────────────────────────────────────────────────────────

        // 1. Service must return a failure result (Requirement 2.3)
        if (result.IsSuccess)
            return false;

        // 2. AddItemToCartAsync must never have been called (Requirement 7.2)
        if (addItemWasCalled)
            return false;

        return true;
    }
}
