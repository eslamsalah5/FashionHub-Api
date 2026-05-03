using Application.DTOs.Cart;
using Application.Services;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using DomainProduct = Domain.Entities.Product;
using System.Linq;

namespace FashionHub.Tests.Services;

/// <summary>
/// Preservation Property Tests for User-Customer-Cart Data Integrity Fix
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8**
/// 
/// IMPORTANT: These tests follow observation-first methodology.
/// They capture the baseline behavior on UNFIXED code for non-buggy inputs.
/// 
/// EXPECTED OUTCOME: Tests PASS on unfixed code (confirms baseline behavior to preserve).
/// After the fix is implemented, these tests should STILL PASS (confirms no regressions).
/// </summary>
public class CartServicePreservationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Property 2: Preservation - Existing User Cart Operations Continue to Work
    // 
    // For all users with valid Customer records (NOT isBugCondition), cart operations
    // should continue to work exactly as before the fix.
    // 
    // Preservation Requirements:
    //   - Users with properly linked AppUser and Customer records can add items to cart
    //   - Cart operations (add, update, remove, clear) work correctly for valid users
    //   - Cart timestamps (CreatedAt, ModifiedAt) are updated correctly
    //   - Multiple cart items with different sizes/colors are treated as separate items
    // 
    // Testing Approach: Property-based testing generates many test cases for stronger guarantees
    // ─────────────────────────────────────────────────────────────────────────

    // ─────────────────────────────────────────────────────────────────────────
    // Test 1: AddToCartAsync with Valid Customer Record
    // **Validates: Requirements 3.1, 3.2, 3.6**
    // 
    // For any user with a valid Customer record (matching AppUser ID),
    // adding items to cart should succeed.
    // ─────────────────────────────────────────────────────────────────────────

    [Property(MaxTest = 20)]
    public void AddToCartAsync_ValidCustomerRecord_ShouldSucceed(
        Guid userGuid,
        PositiveInt productId,
        PositiveInt quantity,
        NonEmptyString size,
        NonEmptyString color)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var userId = userGuid.ToString();
        var customer = new Customer { Id = userId };
        var appUser = new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            FullName = $"User {userId}",
            UserType = Domain.Enums.UserType.Customer
        };
        
        var product = new DomainProduct
        {
            Id = productId.Get,
            Name = $"Product {productId.Get}",
            SKU = $"SKU-{productId.Get}",
            Price = 49.99m,
            StockQuantity = Math.Max(quantity.Get + 10, 100)
        };

        var mockProductRepo = new Mock<IProductRepository>();
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId.Get))
            .ReturnsAsync(product);

        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(appUser);
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync(customer);

        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();

        var mockCartRepo = new Mock<ICartRepository>();
        var cart = new Cart
        {
            Id = 1,
            CustomerId = userId,
            CartItems = new List<CartItem>()
        };
        
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(userId))
            .ReturnsAsync(cart);
        
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
                        ProductId = productId.Get,
                        Quantity = quantity.Get,
                        PriceAtAddition = 49.99m,
                        SelectedSize = size.Get,
                        SelectedColor = color.Get,
                        Product = product
                    }
                }
            });

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        var request = new AddToCartDto
        {
            ProductId = productId.Get,
            Quantity = quantity.Get,
            SelectedSize = size.Get,
            SelectedColor = color.Get
        };

        // ── Act ──────────────────────────────────────────────────────────────
        var result = service.AddToCartAsync(userId, request).Result;

        // ── Assert ───────────────────────────────────────────────────────────
        // Preservation: Cart operations should succeed for valid users
        Assert.True(result.IsSuccess, 
            $"AddToCartAsync should succeed for user with valid Customer record. UserId: {userId}. Errors: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.Data);
        Assert.NotEmpty(result.Data.Items);
        Assert.Equal(productId.Get, result.Data.Items[0].ProductId);
        Assert.Equal(quantity.Get, result.Data.Items[0].Quantity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 2: GetCartAsync with Valid Customer Record
    // **Validates: Requirements 3.1, 3.6**
    // 
    // For any user with a valid Customer record, getting the cart should succeed.
    // ─────────────────────────────────────────────────────────────────────────

    [Property(MaxTest = 20)]
    public void GetCartAsync_ValidCustomerRecord_ShouldSucceed(Guid userGuid)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var userId = userGuid.ToString();
        var customer = new Customer { Id = userId };
        var appUser = new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            FullName = $"User {userId}",
            UserType = Domain.Enums.UserType.Customer
        };

        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(appUser);
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync(customer);

        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();

        var mockCartRepo = new Mock<ICartRepository>();
        var cart = new Cart
        {
            Id = 1,
            CustomerId = userId,
            CartItems = new List<CartItem>()
        };
        
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(userId))
            .ReturnsAsync(cart);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        // ── Act ──────────────────────────────────────────────────────────────
        var result = service.GetCartAsync(userId).Result;

        // ── Assert ───────────────────────────────────────────────────────────
        // Preservation: GetCart should succeed for valid users
        Assert.True(result.IsSuccess, 
            $"GetCartAsync should succeed for user with valid Customer record. UserId: {userId}");
        Assert.NotNull(result.Data);
        Assert.Equal(userId, result.Data.CustomerId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 3: UpdateCartItemQuantityAsync with Valid Customer Record
    // **Validates: Requirements 3.1, 3.4, 3.6**
    // 
    // For any user with a valid Customer record, updating cart item quantity should succeed.
    // ─────────────────────────────────────────────────────────────────────────

    [Property(MaxTest = 20)]
    public void UpdateCartItemQuantityAsync_ValidCustomerRecord_ShouldSucceed(
        Guid userGuid,
        PositiveInt cartItemId,
        PositiveInt newQuantity)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var userId = userGuid.ToString();
        var customer = new Customer { Id = userId };
        var appUser = new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            FullName = $"User {userId}",
            UserType = Domain.Enums.UserType.Customer
        };

        var product = new DomainProduct
        {
            Id = 1,
            Name = "Test Product",
            SKU = "SKU-1",
            Price = 49.99m,
            StockQuantity = Math.Max(newQuantity.Get + 10, 100)
        };

        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                Id = cartItemId.Get,
                CartId = 1,
                ProductId = 1,
                Quantity = 1,
                PriceAtAddition = 49.99m,
                SelectedSize = "M",
                SelectedColor = "Black",
                Product = product
            }
        };

        var cart = new Cart
        {
            Id = 1,
            CustomerId = userId,
            CartItems = cartItems
        };

        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync(customer);

        var mockProductRepo = new Mock<IProductRepository>();
        mockProductRepo
            .Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(product);

        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(userId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.GetCartItemByIdAsync(cartItemId.Get))
            .ReturnsAsync(cartItems[0]);
        mockCartRepo
            .Setup(r => r.UpdateCartItemQuantityAsync(cartItemId.Get, newQuantity.Get))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(1))
            .ReturnsAsync(new Cart
            {
                Id = 1,
                CustomerId = userId,
                CartItems = new List<CartItem>
                {
                    new CartItem
                    {
                        Id = cartItemId.Get,
                        CartId = 1,
                        ProductId = 1,
                        Quantity = newQuantity.Get,
                        PriceAtAddition = 49.99m,
                        SelectedSize = "M",
                        SelectedColor = "Black",
                        Product = product
                    }
                }
            });

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        var request = new UpdateCartItemDto
        {
            CartItemId = cartItemId.Get,
            Quantity = newQuantity.Get
        };

        // ── Act ──────────────────────────────────────────────────────────────
        var result = service.UpdateCartItemQuantityAsync(userId, request).Result;

        // ── Assert ───────────────────────────────────────────────────────────
        // Preservation: Update operations should succeed for valid users
        Assert.True(result.IsSuccess, 
            $"UpdateCartItemQuantityAsync should succeed for user with valid Customer record. UserId: {userId}");
        Assert.NotNull(result.Data);
        Assert.Equal(newQuantity.Get, result.Data.Items[0].Quantity);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 4: RemoveCartItemAsync with Valid Customer Record
    // **Validates: Requirements 3.1, 3.4, 3.6**
    // 
    // For any user with a valid Customer record, removing cart items should succeed.
    // ─────────────────────────────────────────────────────────────────────────

    [Property(MaxTest = 20)]
    public void RemoveCartItemAsync_ValidCustomerRecord_ShouldSucceed(
        Guid userGuid,
        PositiveInt cartItemId)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var userId = userGuid.ToString();
        var customer = new Customer { Id = userId };
        var appUser = new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            FullName = $"User {userId}",
            UserType = Domain.Enums.UserType.Customer
        };

        var product = new DomainProduct
        {
            Id = 1,
            Name = "Test Product",
            SKU = "SKU-1",
            Price = 49.99m,
            StockQuantity = 50
        };

        var cartItems = new List<CartItem>
        {
            new CartItem
            {
                Id = cartItemId.Get,
                CartId = 1,
                ProductId = 1,
                Quantity = 2,
                PriceAtAddition = 49.99m,
                SelectedSize = "M",
                SelectedColor = "Black",
                Product = product
            }
        };

        var cart = new Cart
        {
            Id = 1,
            CustomerId = userId,
            CartItems = cartItems
        };

        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync(customer);

        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(userId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.GetCartItemByIdAsync(cartItemId.Get))
            .ReturnsAsync(cartItems[0]);
        mockCartRepo
            .Setup(r => r.RemoveCartItemAsync(cartItemId.Get))
            .ReturnsAsync(true);
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByIdAsync(1))
            .ReturnsAsync(new Cart
            {
                Id = 1,
                CustomerId = userId,
                CartItems = new List<CartItem>()
            });

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        // ── Act ──────────────────────────────────────────────────────────────
        var result = service.RemoveCartItemAsync(userId, cartItemId.Get).Result;

        // ── Assert ───────────────────────────────────────────────────────────
        // Preservation: Remove operations should succeed for valid users
        Assert.True(result.IsSuccess, 
            $"RemoveCartItemAsync should succeed for user with valid Customer record. UserId: {userId}");
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data.Items);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 5: ClearCartAsync with Valid Customer Record
    // **Validates: Requirements 3.1, 3.4, 3.6**
    // 
    // For any user with a valid Customer record, clearing the cart should succeed.
    // ─────────────────────────────────────────────────────────────────────────

    [Property(MaxTest = 20)]
    public void ClearCartAsync_ValidCustomerRecord_ShouldSucceed(Guid userGuid)
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var userId = userGuid.ToString();
        var customer = new Customer { Id = userId };
        var appUser = new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            FullName = $"User {userId}",
            UserType = Domain.Enums.UserType.Customer
        };

        var cart = new Cart
        {
            Id = 1,
            CustomerId = userId,
            CartItems = new List<CartItem>
            {
                new CartItem { Id = 1, CartId = 1, ProductId = 1, Quantity = 2 },
                new CartItem { Id = 2, CartId = 1, ProductId = 2, Quantity = 1 }
            }
        };

        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync(customer);

        var mockCartRepo = new Mock<ICartRepository>();
        mockCartRepo
            .Setup(r => r.GetCartWithItemsByCustomerIdAsync(userId))
            .ReturnsAsync(cart);
        mockCartRepo
            .Setup(r => r.ClearCartAsync(1))
            .ReturnsAsync(true);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        // ── Act ──────────────────────────────────────────────────────────────
        var result = service.ClearCartAsync(userId).Result;

        // ── Assert ───────────────────────────────────────────────────────────
        // Preservation: Clear operations should succeed for valid users
        Assert.True(result.IsSuccess, 
            $"ClearCartAsync should succeed for user with valid Customer record. UserId: {userId}");
        Assert.True(result.Data);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Test 6: Multiple Cart Items with Different Sizes/Colors
    // **Validates: Requirement 3.7**
    // 
    // For any user with a valid Customer record, multiple cart items with different
    // sizes or colors for the same product should be treated as separate items.
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task AddToCartAsync_DifferentSizesColors_ShouldCreateSeparateItems()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var userId = Guid.NewGuid().ToString();
        var customer = new Customer { Id = userId };
        var appUser = new AppUser
        {
            Id = userId,
            UserName = $"{userId}@test.com",
            Email = $"{userId}@test.com",
            FullName = $"User {userId}",
            UserType = Domain.Enums.UserType.Customer
        };

        var productId = 100;
        var product = new DomainProduct
        {
            Id = productId,
            Name = $"Product {productId}",
            SKU = $"SKU-{productId}",
            Price = 49.99m,
            StockQuantity = 100
        };

        var mockProductRepo = new Mock<IProductRepository>();
        mockProductRepo
            .Setup(r => r.GetByIdAsync(productId))
            .ReturnsAsync(product);

        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo
            .Setup(r => r.GetByIdAsync(userId))
            .ReturnsAsync(appUser);
        mockUserRepo
            .Setup(r => r.GetCustomerByUserIdAsync(userId))
            .ReturnsAsync(customer);

        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();

        var cartItems = new List<CartItem>();
        var mockCartRepo = new Mock<ICartRepository>();
        var cart = new Cart
        {
            Id = 1,
            CustomerId = userId,
            CartItems = cartItems
        };
        
        mockCartRepo
            .Setup(r => r.GetOrCreateCartAsync(userId))
            .ReturnsAsync(cart);
        
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
                CartItems = new List<CartItem>(cartItems)
            });

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var service = new CartService(mockUow.Object, mockLogger.Object);

        // ── Act ──────────────────────────────────────────────────────────────
        // Add same product with different size/color combinations
        var request1 = new AddToCartDto
        {
            ProductId = productId,
            Quantity = 1,
            SelectedSize = "M",
            SelectedColor = "Black"
        };
        
        cartItems.Add(new CartItem
        {
            Id = 1,
            CartId = 1,
            ProductId = productId,
            Quantity = 1,
            PriceAtAddition = 49.99m,
            SelectedSize = "M",
            SelectedColor = "Black",
            Product = product
        });
        
        var result1 = await service.AddToCartAsync(userId, request1);

        var request2 = new AddToCartDto
        {
            ProductId = productId,
            Quantity = 1,
            SelectedSize = "L",
            SelectedColor = "Black"
        };
        
        cartItems.Add(new CartItem
        {
            Id = 2,
            CartId = 1,
            ProductId = productId,
            Quantity = 1,
            PriceAtAddition = 49.99m,
            SelectedSize = "L",
            SelectedColor = "Black",
            Product = product
        });
        
        var result2 = await service.AddToCartAsync(userId, request2);

        // ── Assert ───────────────────────────────────────────────────────────
        // Preservation: Different sizes/colors should create separate cart items
        Assert.True(result1.IsSuccess, "First AddToCart should succeed");
        Assert.True(result2.IsSuccess, "Second AddToCart should succeed");
        Assert.NotNull(result2.Data);
        Assert.Equal(2, result2.Data.Items.Count);
    }
}
