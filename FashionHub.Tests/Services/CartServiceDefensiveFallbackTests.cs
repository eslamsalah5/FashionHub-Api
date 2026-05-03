using Application.DTOs.Cart;
using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Unit tests for CartService defensive fallback logic
/// Tests cover Task 7.1, 7.2, and 7.3 from the bugfix spec
/// Validates: Requirements 2.2, 2.3, 2.5, 3.1, 3.2, 3.6
/// </summary>
public class CartServiceDefensiveFallbackTests
{
    private const string UserId = "user-defensive-test";
    private const string CustomerId = "customer-defensive-test";
    private const int CartId = 1;

    private static (
        Mock<IUnitOfWork> mockUow,
        Mock<IUserRepository> mockUserRepo,
        Mock<IGenericRepository<Customer>> mockCustomerRepo,
        Mock<ICartRepository> mockCartRepo,
        Mock<IProductRepository> mockProductRepo,
        Mock<ILogger<CartService>> mockLogger,
        CartService service)
        BuildService()
    {
        var mockUserRepo = new Mock<IUserRepository>();
        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();
        var mockCartRepo = new Mock<ICartRepository>();
        var mockProductRepo = new Mock<IProductRepository>();

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        return (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, mockProductRepo, mockLogger, service);
    }

    #region Task 7.1: Test GetCartAsync with missing Customer record

    [Fact]
    public async Task GetCartAsync_MissingCustomerRecord_CreatesCustomerAutomatically()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, _, mockLogger, service) = BuildService();

        // Customer record is missing
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync((Customer?)null);

        // But AppUser exists
        var appUser = new AppUser { Id = UserId, Email = "test@example.com" };
        mockUserRepo
            .Setup(r => r.GetByIdAsync(UserId))
            .ReturnsAsync(appUser);

        // Mock Customer creation
        Customer? capturedCustomer = null;
        mockCustomerRepo
            .Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Callback<Customer>(c => capturedCustomer = c)
            .Returns(Task.CompletedTask);

        // Mock cart retrieval
        var cart = new Cart
        {
            Id = CartId,
            CustomerId = UserId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(UserId))
            .ReturnsAsync(cart);

        // Act
        var result = await service.GetCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(capturedCustomer);
        Assert.Equal(UserId, capturedCustomer.Id); // Customer ID must match AppUser ID
        mockCustomerRepo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Once);
        mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetCartAsync_MissingCustomerRecord_RetrievesCartSuccessfully()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, _, mockLogger, service) = BuildService();

        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync((Customer?)null);

        var appUser = new AppUser { Id = UserId, Email = "test@example.com" };
        mockUserRepo
            .Setup(r => r.GetByIdAsync(UserId))
            .ReturnsAsync(appUser);

        mockCustomerRepo
            .Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Returns(Task.CompletedTask);

        var cart = new Cart
        {
            Id = CartId,
            CustomerId = UserId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(UserId))
            .ReturnsAsync(cart);

        // Act
        var result = await service.GetCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(CartId, result.Data.Id);
    }

    [Fact]
    public async Task GetCartAsync_MissingCustomerRecord_GeneratesWarningLog()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, _, mockLogger, service) = BuildService();

        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync((Customer?)null);

        var appUser = new AppUser { Id = UserId, Email = "test@example.com" };
        mockUserRepo
            .Setup(r => r.GetByIdAsync(UserId))
            .ReturnsAsync(appUser);

        mockCustomerRepo
            .Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Returns(Task.CompletedTask);

        var cart = new Cart
        {
            Id = CartId,
            CustomerId = UserId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(UserId))
            .ReturnsAsync(cart);

        // Act
        var result = await service.GetCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify WARNING log was generated
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("CART_DEFENSIVE_FALLBACK")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
        
        // Verify INFO log was generated for successful creation
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("CART_DEFENSIVE_FALLBACK_SUCCESS")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Task 7.2: Test AddToCartAsync with missing Customer record

    [Fact]
    public async Task AddToCartAsync_MissingCustomerRecord_CreatesCustomerAutomatically()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, mockProductRepo, mockLogger, service) = BuildService();

        const int productId = 55;
        const int requestedQty = 2;

        // Customer record is missing
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync((Customer?)null);

        // But AppUser exists
        var appUser = new AppUser { Id = UserId, Email = "test@example.com" };
        mockUserRepo
            .Setup(r => r.GetByIdAsync(UserId))
            .ReturnsAsync(appUser);

        // Mock Customer creation
        Customer? capturedCustomer = null;
        mockCustomerRepo
            .Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Callback<Customer>(c => capturedCustomer = c)
            .Returns(Task.CompletedTask);

        // Mock product
        var product = new DomainProduct
        {
            Id = productId,
            Name = "Test Product",
            SKU = "SKU-55",
            Price = 49.99m,
            StockQuantity = 10
        };
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        // Mock cart operations
        var cart = new Cart
        {
            Id = CartId,
            CustomerId = UserId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(UserId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.AddItemToCartAsync(CartId, productId, requestedQty, "L", "Blue"))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(CartId))
            .ReturnsAsync(cart);

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
        Assert.NotNull(capturedCustomer);
        Assert.Equal(UserId, capturedCustomer.Id);
        mockCustomerRepo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Once);
    }

    [Fact]
    public async Task AddToCartAsync_MissingCustomerRecord_AddsItemSuccessfully()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, mockProductRepo, mockLogger, service) = BuildService();

        const int productId = 55;
        const int requestedQty = 2;

        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync((Customer?)null);

        var appUser = new AppUser { Id = UserId, Email = "test@example.com" };
        mockUserRepo
            .Setup(r => r.GetByIdAsync(UserId))
            .ReturnsAsync(appUser);

        mockCustomerRepo
            .Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Returns(Task.CompletedTask);

        var product = new DomainProduct
        {
            Id = productId,
            Name = "Test Product",
            SKU = "SKU-55",
            Price = 49.99m,
            StockQuantity = 10
        };
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        var cart = new Cart
        {
            Id = CartId,
            CustomerId = UserId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(UserId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.AddItemToCartAsync(CartId, productId, requestedQty, "L", "Blue"))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(CartId))
            .ReturnsAsync(cart);

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
        mockCartRepo.Verify(r => r.AddItemToCartAsync(CartId, productId, requestedQty, "L", "Blue"), Times.Once);
    }

    [Fact]
    public async Task AddToCartAsync_MissingCustomerRecord_GeneratesWarningLog()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, mockProductRepo, mockLogger, service) = BuildService();

        const int productId = 55;
        const int requestedQty = 2;

        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync((Customer?)null);

        var appUser = new AppUser { Id = UserId, Email = "test@example.com" };
        mockUserRepo
            .Setup(r => r.GetByIdAsync(UserId))
            .ReturnsAsync(appUser);

        mockCustomerRepo
            .Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Returns(Task.CompletedTask);

        var product = new DomainProduct
        {
            Id = productId,
            Name = "Test Product",
            SKU = "SKU-55",
            Price = 49.99m,
            StockQuantity = 10
        };
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        var cart = new Cart
        {
            Id = CartId,
            CustomerId = UserId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(UserId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.AddItemToCartAsync(CartId, productId, requestedQty, "L", "Blue"))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(CartId))
            .ReturnsAsync(cart);

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
        
        // Verify WARNING log was generated
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("CART_DEFENSIVE_FALLBACK")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    #endregion

    #region Task 7.3: Test cart operations with valid Customer records

    [Fact]
    public async Task GetCartAsync_ValidCustomerRecord_NoWarningLogsGenerated()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, _, mockLogger, service) = BuildService();

        // Customer record exists
        var customer = new Customer { Id = CustomerId };
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync(customer);

        var cart = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(CustomerId))
            .ReturnsAsync(cart);

        // Act
        var result = await service.GetCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        
        // Verify NO WARNING logs were generated (defensive fallback not triggered)
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("CART_DEFENSIVE_FALLBACK")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
        
        // Verify Customer was NOT created
        mockCustomerRepo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public async Task AddToCartAsync_ValidCustomerRecord_NoWarningLogsGenerated()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, mockProductRepo, mockLogger, service) = BuildService();

        const int productId = 55;
        const int requestedQty = 2;

        // Customer record exists
        var customer = new Customer { Id = CustomerId };
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync(customer);

        var product = new DomainProduct
        {
            Id = productId,
            Name = "Test Product",
            SKU = "SKU-55",
            Price = 49.99m,
            StockQuantity = 10
        };
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        var cart = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>()
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(CustomerId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.AddItemToCartAsync(CartId, productId, requestedQty, "L", "Blue"))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(CartId))
            .ReturnsAsync(cart);

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
        
        // Verify NO WARNING logs were generated
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("CART_DEFENSIVE_FALLBACK")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
        
        // Verify Customer was NOT created
        mockCustomerRepo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Never);
    }

    [Fact]
    public async Task CartOperations_ValidCustomerRecord_ContinueToWorkNormally()
    {
        // Arrange
        var (mockUow, mockUserRepo, mockCustomerRepo, mockCartRepo, _, mockLogger, service) = BuildService();

        var customer = new Customer { Id = CustomerId };
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(UserId))
            .ReturnsAsync(customer);

        var cart = new Cart
        {
            Id = CartId,
            CustomerId = CustomerId,
            CartItems = new List<CartItem>
            {
                new CartItem
                {
                    Id = 1,
                    CartId = CartId,
                    ProductId = 10,
                    Quantity = 2,
                    PriceAtAddition = 29.99m,
                    SelectedSize = "M",
                    SelectedColor = "Black"
                }
            }
        };
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(CustomerId))
            .ReturnsAsync(cart);

        // Act
        var result = await service.GetCartAsync(UserId);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal(2, result.Data.Items[0].Quantity);
        
        // Verify normal operation - no defensive fallback triggered
        mockCustomerRepo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Never);
    }

    #endregion
}
