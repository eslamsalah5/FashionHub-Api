using Application.DTOs.Cart;
using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Unit tests for CartService operations.
/// Validates: Requirements 2.1, 2.7, 2.8
/// </summary>
public class CartServiceTests
{
    private const string UserId = "user-unit-test";
    private const string CustomerId = "customer-unit-test";
    private const int CartId = 1;

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static (
        Mock<IUnitOfWork> mockUow,
        Mock<IUserRepository> mockUserRepo,
        Mock<ICartRepository> mockCartRepo,
        Mock<IProductRepository> mockProductRepo,
        CartService service)
        BuildService()
    {
        var mockUserRepo = new Mock<IUserRepository>();
        var mockCartRepo = new Mock<ICartRepository>();
        var mockProductRepo = new Mock<IProductRepository>();

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Default: user resolves to customer
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync(new Customer { Id = CustomerId });

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        return (mockUow, mockUserRepo, mockCartRepo, mockProductRepo, service);
    }

    private static Cart BuildCartWithItems(int itemCount = 2)
    {
        var items = Enumerable.Range(1, itemCount).Select(i => new CartItem
        {
            Id = i,
            CartId = CartId,
            ProductId = i * 10,
            Quantity = i,
            PriceAtAddition = i * 9.99m,
            SelectedSize = "M",
            SelectedColor = "Black",
            Product = new DomainProduct
            {
                Id = i * 10,
                Name = $"Product {i}",
                SKU = $"SKU-{i}",
                Price = i * 9.99m,
                StockQuantity = 50
            }
        }).ToList();

        return new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = items
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1 — GetCartAsync with valid customer returns cart DTO
    // Validates: Requirement 2.1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartAsync_ValidCustomer_ReturnsCartDto()
    {
        // Arrange
        var (_, _, mockCartRepo, _, service) = BuildService();

        var cart = BuildCartWithItems(2);
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(CustomerId))
            .ReturnsAsync(cart);

        // Act
        var result = await service.GetCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(CartId, result.Data.Id);
        Assert.Equal(CustomerId, result.Data.CustomerId);
        Assert.Equal(2, result.Data.Items.Count);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2 — GetCartAsync with empty cart returns empty cart DTO
    // Validates: Requirement 2.1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartAsync_EmptyCart_ReturnsEmptyCartDto()
    {
        // Arrange
        var (_, _, mockCartRepo, _, service) = BuildService();

        var emptyCart = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(CustomerId))
            .ReturnsAsync(emptyCart);

        // Act
        var result = await service.GetCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        Assert.Equal(0, result.Data.TotalItems);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3 — AddToCartAsync with valid product and sufficient stock adds item
    // Validates: Requirement 2.1, 2.2
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddToCartAsync_ValidProductWithStock_AddsItemAndReturnsUpdatedCart()
    {
        // Arrange
        var (_, _, mockCartRepo, mockProductRepo, service) = BuildService();

        const int productId = 55;
        const int requestedQty = 2;
        const decimal productPrice = 49.99m;

        var product = new DomainProduct
        {
            Id = productId,
            Name = "Blue Jeans",
            SKU = "SKU-55",
            Price = productPrice,
            StockQuantity = 10
        };

        var emptyCart = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>()
        };

        var cartAfterAdd = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>
            {
                new CartItem
                {
                    Id = 1,
                    CartId = CartId,
                    ProductId = productId,
                    Quantity = requestedQty,
                    PriceAtAddition = productPrice,
                    SelectedSize = "L",
                    SelectedColor = "Blue",
                    Product = product
                }
            }
        };

        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(CustomerId))
            .ReturnsAsync(emptyCart);
        mockCartRepo
            .Setup(r => r.AddItemToCartAsync(CartId, productId, requestedQty, "L", "Blue"))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(CartId))
            .ReturnsAsync(cartAfterAdd);

        var request = new AddToCartDto
        {
            ProductId = productId,
            Quantity = requestedQty,
            SelectedSize = "L",
            SelectedColor = "Blue"
        };

        // Act
        var result = await service.AddToCartAsync(UserId, request);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(productId, result.Data.Items[0].ProductId);
        Assert.Equal(requestedQty, result.Data.Items[0].Quantity);
        Assert.Equal(productPrice, result.Data.Items[0].UnitPrice);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4 — AddToCartAsync with quantity exceeding stock returns failure
    // Validates: Requirement 2.3
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddToCartAsync_QuantityExceedsStock_ReturnsFailure()
    {
        // Arrange
        var (_, _, mockCartRepo, mockProductRepo, service) = BuildService();

        const int productId = 77;
        const int stockQty = 3;
        const int requestedQty = 5;  // exceeds stock

        var product = new DomainProduct
        {
            Id = productId,
            Name = "Limited Sneakers",
            SKU = "SKU-77",
            Price = 120.00m,
            StockQuantity = stockQty
        };

        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        var request = new AddToCartDto
        {
            ProductId = productId,
            Quantity = requestedQty,
            SelectedSize = "42",
            SelectedColor = "White"
        };

        // Act
        var result = await service.AddToCartAsync(UserId, request);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("Not enough stock available"));
        // AddItemToCartAsync must never be called
        mockCartRepo.Verify(
            r => r.AddItemToCartAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5 — RemoveCartItemAsync removes the correct item
    // Validates: Requirement 2.7
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveCartItemAsync_ValidItem_RemovesItemAndReturnsUpdatedCart()
    {
        // Arrange
        var (_, _, mockCartRepo, _, service) = BuildService();

        const int cartItemId = 5;

        var cartItem = new CartItem
        {
            Id = cartItemId,
            CartId = CartId,
            ProductId = 10,
            Quantity = 1,
            PriceAtAddition = 29.99m,
            SelectedSize = "S",
            SelectedColor = "Red"
        };

        var cartWithItem = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem> { cartItem }
        };

        var emptyCartAfterRemove = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>()
        };

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync(cartWithItem);
        mockCartRepo
            .Setup(r => r.GetCartItemByIdAsync(cartItemId))
            .ReturnsAsync(cartItem);
        mockCartRepo
            .Setup(r => r.RemoveCartItemAsync(cartItemId))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(CartId))
            .ReturnsAsync(emptyCartAfterRemove);

        // Act
        var result = await service.RemoveCartItemAsync(UserId, cartItemId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
        mockCartRepo.Verify(r => r.RemoveCartItemAsync(cartItemId), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6 — RemoveCartItemAsync with item belonging to another cart returns failure
    // Validates: Requirement 2.9
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveCartItemAsync_ItemBelongsToAnotherCart_ReturnsFailure()
    {
        // Arrange
        var (_, _, mockCartRepo, _, service) = BuildService();

        const int cartItemId = 99;
        const int otherCartId = 999;  // different cart

        var cartItem = new CartItem
        {
            Id = cartItemId,
            CartId = otherCartId,  // belongs to a different cart
            ProductId = 10,
            Quantity = 1,
            PriceAtAddition = 29.99m
        };

        var customerCart = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>()
        };

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync(customerCart);
        mockCartRepo
            .Setup(r => r.GetCartItemByIdAsync(cartItemId))
            .ReturnsAsync(cartItem);

        // Act
        var result = await service.RemoveCartItemAsync(UserId, cartItemId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("Cart item does not belong to your cart"));
        mockCartRepo.Verify(r => r.RemoveCartItemAsync(It.IsAny<int>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 7 — ClearCartAsync removes all items
    // Validates: Requirement 2.8
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearCartAsync_CartWithItems_ClearsAllItemsAndReturnsSuccess()
    {
        // Arrange
        var (_, _, mockCartRepo, _, service) = BuildService();

        var cartWithItems = BuildCartWithItems(3);

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync(cartWithItems);
        mockCartRepo
            .Setup(r => r.ClearCartAsync(CartId))
            .ReturnsAsync(true);

        // Act
        var result = await service.ClearCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.True(result.Data);
        mockCartRepo.Verify(r => r.ClearCartAsync(CartId), Times.Once);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 8 — ClearCartAsync when no cart exists returns success (no-op)
    // Validates: Requirement 2.8
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ClearCartAsync_NoCartExists_ReturnsSuccessWithoutCallingClear()
    {
        // Arrange
        var (_, _, mockCartRepo, _, service) = BuildService();

        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(CustomerId))
            .ReturnsAsync((Cart?)null);

        // Act
        var result = await service.ClearCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        mockCartRepo.Verify(r => r.ClearCartAsync(It.IsAny<int>()), Times.Never);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 9 — GetCartItemCountAsync returns correct total quantity
    // Validates: Requirement 2.1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartItemCountAsync_CartWithItems_ReturnsCorrectTotalQuantity()
    {
        // Arrange
        var (_, _, mockCartRepo, _, service) = BuildService();

        const int expectedCount = 7;
        mockCartRepo
            .Setup(r => r.GetCartItemCountAsync(CustomerId))
            .ReturnsAsync(expectedCount);

        // Act
        var result = await service.GetCartItemCountAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedCount, result.Data);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 10 — GetCartItemCountAsync with no customer returns 0
    // Validates: Requirement 2.1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartItemCountAsync_CustomerNotFound_ReturnsZero()
    {
        // Arrange
        var (_, mockUserRepo, _, _, service) = BuildService();

        // Override: customer not found
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync((Customer?)null);

        // Act
        var result = await service.GetCartItemCountAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Data);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 11 — GetCartAsync with unknown user returns failure
    // Validates: Requirement 2.1
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartAsync_CustomerNotFound_ReturnsFailure()
    {
        // Arrange
        var (_, mockUserRepo, _, _, service) = BuildService();

        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync((Customer?)null);
        
        // Mock GetByIdAsync to return null (AppUser doesn't exist either)
        mockUserRepo
            .Setup(r => r.GetByIdAsync(UserId))
            .ReturnsAsync((AppUser?)null);

        // Act
        var result = await service.GetCartAsync(UserId);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Contains("User account not found"));
    }
}
