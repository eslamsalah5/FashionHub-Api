using Application.DTOs.Cart;
using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Bug Condition Exploration Test for User-Customer-Cart Data Integrity Fix
/// **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 2.3**
/// 
/// CRITICAL: This test MUST FAIL on unfixed code - failure confirms the bug exists.
/// DO NOT attempt to fix the test or the code when it fails.
/// 
/// This test encodes the expected behavior - it will validate the fix when it passes after implementation.
/// </summary>
public class CartServiceBugConditionTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Property 1: Bug Condition - Missing Customer Record Causes Cart Operations to Fail
    // 
    // **Scoped PBT Approach**: For deterministic bugs, scope the property to the concrete failing case(s)
    // to ensure reproducibility.
    // 
    // Bug Condition (C): isBugCondition(input) where:
    //   - input.userId IS NOT NULL
    //   - AppUserExists(input.userId) == true
    //   - CustomerExists(input.userId) == false
    //   - input.operation IN ['GetCart', 'AddToCart', 'UpdateCart', 'RemoveFromCart', 'ClearCart']
    // 
    // Expected Behavior (P): Cart operations should succeed by creating missing Customer records
    //   - Property 1: Cart operations should succeed by creating missing Customer records
    //   - Property 2: Customer record should be created with ID matching AppUser ID
    // 
    // GOAL: Surface counterexamples that demonstrate the bug exists
    // 
    // Expected Counterexamples:
    //   - "AddToCartAsync fails with 'Customer account not found' when Customer record is missing"
    //   - "GetCustomerByUserIdAsync returns null for AppUser without Customer record"
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    // Property 1: Bug Condition - Missing Customer Record Causes Cart Operations to Fail
    // 
    // **Scoped PBT Approach**: For deterministic bugs, we test the concrete failing case
    // to ensure reproducibility.
    // 
    // Bug Condition (C): isBugCondition(input) where:
    //   - input.userId IS NOT NULL
    //   - AppUserExists(input.userId) == true
    //   - CustomerExists(input.userId) == false
    //   - input.operation IN ['GetCart', 'AddToCart', 'UpdateCart', 'RemoveFromCart', 'ClearCart']
    // 
    // Expected Behavior (P): Cart operations should succeed by creating missing Customer records
    //   - Property 1: Cart operations should succeed by creating missing Customer records
    //   - Property 2: Customer record should be created with ID matching AppUser ID
    // 
    // GOAL: Surface counterexamples that demonstrate the bug exists
    // 
    // Expected Counterexamples:
    //   - "AddToCartAsync fails with 'Customer account not found' when Customer record is missing"
    //   - "GetCustomerByUserIdAsync returns null for AppUser without Customer record"
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: AddToCartAsync with Missing Customer Record
    // **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 2.3**
    // 
    // For any AddToCartAsync call where:
    //   - AppUser exists with the given userId
    //   - Customer record does NOT exist for that userId
    // 
    // Expected Behavior (after fix):
    //   - The service should create the missing Customer record with ID = userId
    //   - The cart operation should succeed
    //   - The item should be added to the cart
    // 
    // Current Behavior (unfixed code - THIS TEST WILL FAIL):
    //   - GetCustomerByUserIdAsync returns null
    //   - AddToCartAsync returns failure with "Customer account not found"
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddToCartAsync_MissingCustomerRecord_ShouldCreateCustomerAndSucceed()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string userId = "bba2bd4a-43bf-4d33-918a-d9febd321e0c";
        const int productId = 100;
        const int requestedQty = 2;
        const decimal productPrice = 49.99m;

        // Product with sufficient stock
        var product = new DomainProduct
        {
            Id = productId,
            Name = "Test Product",
            SKU = "SKU-100",
            Price = productPrice,
            StockQuantity = 50
        };

        // AppUser exists (simulated by the fact that we have a userId)
        var appUser = new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            FullName = "Test User",
            UserType = Domain.Enums.UserType.Customer
        };

        // Track whether Customer was created
        Customer? createdCustomer = null;
        bool customerWasCreated = false;

        // Mock IProductRepository — returns the product
        var mockProductRepo = new Mock<IProductRepository>();
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        // Mock IUserRepository — simulates the bug condition
        // Initially returns null (Customer record missing)
        // After fix, the CartService should create the Customer record
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(appUser);
        
        // BUG CONDITION: GetCustomerByUserIdAsync returns null (Customer record missing)
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync((Customer?)null);

        // Mock IGenericRepository<Customer> — tracks Customer creation
        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();
        mockCustomerRepo
            .Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Callback<Customer>(c =>
            {
                createdCustomer = c;
                customerWasCreated = true;
                
                // After Customer is created, update the mock to return it
                mockUserRepo
                    .Setup(r => r.GetCustomerByUserIdAsync(userId))
                    .ReturnsAsync(c);
            })
            .Returns(Task.CompletedTask);

        // Mock ICartRepository — returns a cart after Customer is created
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(It.IsAny<string>()))
            .ReturnsAsync((string customerId) => new Cart
            {
                Id = 1,
                CustomerId = customerId,
                CartItems = new List<CartItem>()
            });
        
        mockCartRepo
            .Setup(r => r.AddItemToCartAsync(
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<int>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .ReturnsAsync(true);
        
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int cartId) => new Cart
            {
                Id = cartId,
                CustomerId = userId,
                CartItems = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = 1,
                        CartId = cartId,
                        ProductId = productId,
                        Quantity = requestedQty,
                        PriceAtAddition = productPrice,
                        SelectedSize = "M",
                        SelectedColor = "Black",
                        Product = product
                    }
                }
            });

        // Mock IUnitOfWork
        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Mock ILogger<CartService>
        var mockLogger = new Mock<ILogger<CartService>>();

        var service = new CartService(mockUow.Object, mockLogger.Object);

        var request = new AddToCartDto
        {
            ProductId = productId,
            Quantity = requestedQty,
            SelectedSize = "M",
            SelectedColor = "Black"
        };

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await service.AddToCartAsync(userId, request);

        // ── Assert ────────────────────────────────────────────────────────────
        // Expected Behavior (after fix):
        // 1. The service should succeed (Requirement 2.2)
        Assert.True(result.IsSuccess, 
            "AddToCartAsync should succeed after creating missing Customer record. " +
            $"Actual errors: {string.Join(", ", result.Errors)}");

        // 2. Customer record should have been created (Requirement 2.1)
        Assert.True(customerWasCreated, 
            "Customer record should be created automatically when missing");

        // 3. Customer ID should match AppUser ID (Requirement 2.3)
        Assert.NotNull(createdCustomer);
        Assert.Equal(userId, createdCustomer.Id);

        // 4. Cart operation should succeed and item should be added
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.Items);
        Assert.Equal(productId, result.Data.Items[0].ProductId);
        Assert.Equal(requestedQty, result.Data.Items[0].Quantity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Additional Test: GetCartAsync with Missing Customer Record
    // **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 2.3**
    // 
    // This test validates the same bug condition for GetCartAsync operation
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetCartAsync_MissingCustomerRecord_ShouldCreateCustomerAndSucceed()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        const string userId = "user-missing-customer-getcart";

        // AppUser exists
        var appUser = new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            FullName = "Test User",
            UserType = Domain.Enums.UserType.Customer
        };

        // Track whether Customer was created
        Customer? createdCustomer = null;
        bool customerWasCreated = false;

        // Mock IUserRepository
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(appUser);
        
        // BUG CONDITION: GetCustomerByUserIdAsync returns null
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync((Customer?)null);

        // Mock IGenericRepository<Customer>
        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();
        mockCustomerRepo
            .Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Callback<Customer>(c =>
            {
                createdCustomer = c;
                customerWasCreated = true;
                
                // After Customer is created, update the mock to return it
                mockUserRepo
                    .Setup(r => r.GetCustomerByUserIdAsync(userId))
                    .ReturnsAsync(c);
            })
            .Returns(Task.CompletedTask);

        // Mock ICartRepository
        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(It.IsAny<string>()))
            .ReturnsAsync((string customerId) => new Cart
            {
                Id = 1,
                CustomerId = customerId,
                CartItems = new List<CartItem>()
            });

        // Mock IUnitOfWork
        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        // Mock ILogger<CartService>
        var mockLogger = new Mock<ILogger<CartService>>();

        var service = new CartService(mockUow.Object, mockLogger.Object);

        // ── Act ───────────────────────────────────────────────────────────────
        var result = await service.GetCartAsync(userId);

        // ── Assert ────────────────────────────────────────────────────────────
        // Expected Behavior (after fix):
        // 1. The service should succeed
        Assert.True(result.IsSuccess, "GetCartAsync should succeed after creating missing Customer record");

        // 2. Customer record should have been created
        Assert.True(customerWasCreated, "Customer record should be created automatically");

        // 3. Customer ID should match AppUser ID
        Assert.NotNull(createdCustomer);
        Assert.Equal(userId, createdCustomer.Id);

        // 4. Cart should be returned
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.CustomerId);
    }
}
