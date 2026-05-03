using Application.DTOs.Auth;
using Application.DTOs.Cart;
using Application.Services;
using Application.Services.Auth;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using DomainProduct = Domain.Entities.Product;

namespace FashionHub.Tests.Services;

/// <summary>
/// Integration-style tests for full registration and cart flow
/// Tests cover Task 8.1, 8.2, and 8.4 from the bugfix spec
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.6**
/// </summary>
public class RegistrationAndCartFlowIntegrationTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Task 8.1: Test full registration flow from API to database
    // **Validates: Requirements 2.1, 2.4**
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullRegistrationFlow_WithValidCustomerData_CreatesBothAppUserAndCustomerWithMatchingIds()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var customerDto = new CustomerDto
        {
            Email = "newuser@example.com",
            Password = "SecurePass@123",
            FullName = "New User",
            PhoneNumber = "1234567890",
            DateOfBirth = new DateTime(1990, 5, 15),
            Address = "123 Main St, City, State"
        };

        AppUser capturedAppUser = null;
        Customer capturedCustomer = null;

        // Setup UserManager mock
        var userStore = new Mock<IUserStore<AppUser>>();
        var mockUserManager = new Mock<UserManager<AppUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        mockUserManager.Setup(um => um.FindByEmailAsync(customerDto.Email))
            .ReturnsAsync((AppUser)null);
        
        mockUserManager.Setup(um => um.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .Callback<AppUser, string>((user, pwd) => capturedAppUser = user)
            .ReturnsAsync(IdentityResult.Success);
        
        mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<AppUser>(), "Customer"))
            .ReturnsAsync(IdentityResult.Success);

        // Setup SignInManager mock
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        var mockSignInManager = new Mock<SignInManager<AppUser>>(
            mockUserManager.Object,
            contextAccessor.Object,
            userPrincipalFactory.Object,
            null, null, null, null);

        // Setup other service mocks
        var mockJwtService = new Mock<IJwtService>();
        var mockEmailService = new Mock<IEmailService>();
        var mockFileService = new Mock<IFileService>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<AuthService>>();

        // Setup UnitOfWork transaction methods
        mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.CommitTransactionAsync()).Returns(Task.CompletedTask);
        
        // Setup Customer repository
        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();
        mockCustomerRepo.Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .Callback<Customer>(c => capturedCustomer = c)
            .Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);

        var authService = new AuthService(
            mockUserManager.Object,
            mockSignInManager.Object,
            mockJwtService.Object,
            mockEmailService.Object,
            mockFileService.Object,
            mockUnitOfWork.Object,
            mockLogger.Object
        );

        // ── Act ──────────────────────────────────────────────────────────────
        var result = await authService.RegisterCustomerAsync(customerDto);

        // ── Assert ───────────────────────────────────────────────────────────
        // Verify registration succeeded
        Assert.True(result.IsSuccess, $"Registration should succeed. Errors: {string.Join(", ", result.Errors)}");
        
        // Verify both AppUser and Customer were created
        Assert.NotNull(capturedAppUser);
        Assert.NotNull(capturedCustomer);
        
        // Verify IDs match between AppUser and Customer (CRITICAL for data integrity)
        Assert.Equal(capturedAppUser.Id, capturedCustomer.Id);
        
        // Verify AppUser properties
        Assert.Equal(customerDto.Email, capturedAppUser.Email);
        Assert.Equal(customerDto.FullName, capturedAppUser.FullName);
        Assert.Equal(customerDto.PhoneNumber, capturedAppUser.PhoneNumber);
        Assert.Equal(customerDto.DateOfBirth, capturedAppUser.DateOfBirth);
        Assert.Equal(customerDto.Address, capturedAppUser.Address);
        Assert.Equal(UserType.Customer, capturedAppUser.UserType);
        
        // Verify transaction was committed
        mockUnitOfWork.Verify(u => u.BeginTransactionAsync(), Times.Once);
        mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Once);
        mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(), Times.Never);
        
        // Verify role was assigned
        mockUserManager.Verify(um => um.AddToRoleAsync(It.IsAny<AppUser>(), "Customer"), Times.Once);
    }

    [Fact]
    public async Task FullRegistrationFlow_UserCanLoginAfterRegistration()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var customerDto = new CustomerDto
        {
            Email = "logintest@example.com",
            Password = "SecurePass@123",
            FullName = "Login Test User",
            PhoneNumber = "9876543210",
            DateOfBirth = new DateTime(1985, 3, 20),
            Address = "456 Oak Ave"
        };

        var registeredAppUser = new AppUser
        {
            Id = Guid.NewGuid().ToString(),
            Email = customerDto.Email,
            UserName = customerDto.Email,
            FullName = customerDto.FullName,
            UserType = UserType.Customer
        };

        // Setup UserManager mock
        var userStore = new Mock<IUserStore<AppUser>>();
        var mockUserManager = new Mock<UserManager<AppUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        // Registration phase - first call returns null (user doesn't exist)
        var findByEmailCallCount = 0;
        mockUserManager.Setup(um => um.FindByEmailAsync(customerDto.Email))
            .ReturnsAsync(() =>
            {
                findByEmailCallCount++;
                return findByEmailCallCount == 1 ? null : registeredAppUser;
            });
        
        mockUserManager.Setup(um => um.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .Callback<AppUser, string>((user, pwd) => registeredAppUser = user)
            .ReturnsAsync(IdentityResult.Success);
        mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<AppUser>(), "Customer"))
            .ReturnsAsync(IdentityResult.Success);

        // Login phase
        mockUserManager.Setup(um => um.CheckPasswordAsync(It.IsAny<AppUser>(), customerDto.Password))
            .ReturnsAsync(true);
        mockUserManager.Setup(um => um.GetRolesAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(new List<string> { "Customer" });

        // Setup SignInManager mock
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        var mockSignInManager = new Mock<SignInManager<AppUser>>(
            mockUserManager.Object,
            contextAccessor.Object,
            userPrincipalFactory.Object,
            null, null, null, null);

        mockSignInManager.Setup(sm => sm.CheckPasswordSignInAsync(
            It.IsAny<AppUser>(), 
            It.IsAny<string>(), 
            false))
            .ReturnsAsync(SignInResult.Success);

        // Setup other service mocks
        var mockJwtService = new Mock<IJwtService>();
        mockJwtService.Setup(j => j.GenerateTokenAsync(It.IsAny<AppUser>()))
            .ReturnsAsync("mock-jwt-token");

        var mockEmailService = new Mock<IEmailService>();
        var mockFileService = new Mock<IFileService>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<AuthService>>();

        mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.CommitTransactionAsync()).Returns(Task.CompletedTask);
        
        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();
        mockCustomerRepo.Setup(r => r.AddAsync(It.IsAny<Customer>())).Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);

        var authService = new AuthService(
            mockUserManager.Object,
            mockSignInManager.Object,
            mockJwtService.Object,
            mockEmailService.Object,
            mockFileService.Object,
            mockUnitOfWork.Object,
            mockLogger.Object
        );

        // ── Act ──────────────────────────────────────────────────────────────
        // Step 1: Register
        var registrationResult = await authService.RegisterCustomerAsync(customerDto);
        
        // Step 2: Login
        var loginDto = new LoginDto
        {
            EmailOrUsername = customerDto.Email,
            Password = customerDto.Password
        };
        var loginResult = await authService.LoginAsync(loginDto);

        // ── Assert ───────────────────────────────────────────────────────────
        Assert.True(registrationResult.IsSuccess, "Registration should succeed");
        Assert.True(loginResult.IsSuccess, "Login should succeed after registration");
        Assert.NotNull(loginResult.Data);
        Assert.Equal("mock-jwt-token", loginResult.Data.Token);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Task 8.2: Test full cart flow for new users
    // **Validates: Requirements 2.2, 2.3, 3.1, 3.2, 3.6**
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task FullCartFlow_NewlyRegisteredUser_CanAddUpdateAndCheckoutCart()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var userId = Guid.NewGuid().ToString();
        var customer = new Customer { Id = userId };
        var appUser = new AppUser
        {
            Id = userId,
            Email = "carttest@example.com",
            UserName = "carttest@example.com",
            FullName = "Cart Test User",
            UserType = UserType.Customer
        };

        var product1 = new DomainProduct
        {
            Id = 1,
            Name = "Blue Jeans",
            SKU = "SKU-001",
            Price = 59.99m,
            StockQuantity = 50
        };

        var product2 = new DomainProduct
        {
            Id = 2,
            Name = "White T-Shirt",
            SKU = "SKU-002",
            Price = 19.99m,
            StockQuantity = 100
        };

        // Setup mocks
        var mockUserRepo = new Mock<IUserRepository>();
        mockUserRepo.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(appUser);
        mockUserRepo.Setup(r => r.GetCustomerByUserIdAsync(userId)).ReturnsAsync(customer);

        var mockProductRepo = new Mock<IProductRepository>();
        mockProductRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(product1);
        mockProductRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(product2);

        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();

        var cartItems = new List<CartItem>();
        var mockCartRepo = new Mock<ICartRepository>();
        var cart = new Cart
        {
            Id = 1,
            CustomerId = userId,
            CartItems = cartItems
        };

        mockCartRepo.Setup(r => r.GetOrCreateCartAsync(userId)).ReturnsAsync(cart);
        mockCartRepo.Setup(r => r.GetCartWithItemsByCustomerIdAsync(userId)).ReturnsAsync(cart);
        mockCartRepo.Setup(r => r.AddItemToCartAsync(
            It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), 
            It.IsAny<string>(), It.IsAny<string>()))
            .Callback<int, int, int, string, string>((cartId, prodId, qty, size, color) =>
            {
                var product = prodId == 1 ? product1 : product2;
                cartItems.Add(new CartItem
                {
                    Id = cartItems.Count + 1,
                    CartId = cartId,
                    ProductId = prodId,
                    Quantity = qty,
                    PriceAtAddition = product.Price,
                    SelectedSize = size,
                    SelectedColor = color,
                    Product = product
                });
            })
            .ReturnsAsync(true);

        mockCartRepo.Setup(r => r.GetCartWithItemsByIdAsync(1))
            .ReturnsAsync(() => new Cart
            {
                Id = 1,
                CustomerId = userId,
                CartItems = new List<CartItem>(cartItems)
            });

        mockCartRepo.Setup(r => r.GetCartItemByIdAsync(It.IsAny<int>()))
            .ReturnsAsync((int itemId) => cartItems.FirstOrDefault(ci => ci.Id == itemId));

        mockCartRepo.Setup(r => r.UpdateCartItemQuantityAsync(It.IsAny<int>(), It.IsAny<int>()))
            .Callback<int, int>((itemId, newQty) =>
            {
                var item = cartItems.FirstOrDefault(ci => ci.Id == itemId);
                if (item != null) item.Quantity = newQty;
            })
            .ReturnsAsync(true);

        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.Users).Returns(mockUserRepo.Object);
        mockUow.Setup(u => u.Products).Returns(mockProductRepo.Object);
        mockUow.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);
        mockUow.Setup(u => u.Carts).Returns(mockCartRepo.Object);
        mockUow.Setup(u => u.SaveChangesAsync()).ReturnsAsync(1);

        var mockLogger = new Mock<ILogger<CartService>>();
        var cartService = new CartService(mockUow.Object, mockLogger.Object);

        // ── Act ──────────────────────────────────────────────────────────────
        // Step 1: Add first item to cart
        var addRequest1 = new AddToCartDto
        {
            ProductId = 1,
            Quantity = 2,
            SelectedSize = "32",
            SelectedColor = "Blue"
        };
        var addResult1 = await cartService.AddToCartAsync(userId, addRequest1);

        // Step 2: Add second item to cart
        var addRequest2 = new AddToCartDto
        {
            ProductId = 2,
            Quantity = 3,
            SelectedSize = "M",
            SelectedColor = "White"
        };
        var addResult2 = await cartService.AddToCartAsync(userId, addRequest2);

        // Step 3: Update quantity of first item
        var updateRequest = new UpdateCartItemDto
        {
            CartItemId = 1,
            Quantity = 3
        };
        var updateResult = await cartService.UpdateCartItemQuantityAsync(userId, updateRequest);

        // Step 4: Get cart to verify state
        var getCartResult = await cartService.GetCartAsync(userId);

        // ── Assert ───────────────────────────────────────────────────────────
        // Verify all operations succeeded
        Assert.True(addResult1.IsSuccess, "First add to cart should succeed");
        Assert.True(addResult2.IsSuccess, "Second add to cart should succeed");
        Assert.True(updateResult.IsSuccess, "Update cart item should succeed");
        Assert.True(getCartResult.IsSuccess, "Get cart should succeed");

        // Verify cart state
        Assert.NotNull(getCartResult.Data);
        Assert.Equal(2, getCartResult.Data.Items.Count);
        
        // Verify first item was updated
        var item1 = getCartResult.Data.Items.FirstOrDefault(i => i.ProductId == 1);
        Assert.NotNull(item1);
        Assert.Equal(3, item1.Quantity); // Updated from 2 to 3
        Assert.Equal(59.99m, item1.UnitPrice);
        
        // Verify second item
        var item2 = getCartResult.Data.Items.FirstOrDefault(i => i.ProductId == 2);
        Assert.NotNull(item2);
        Assert.Equal(3, item2.Quantity);
        Assert.Equal(19.99m, item2.UnitPrice);

        // Verify total
        var expectedTotal = (3 * 59.99m) + (3 * 19.99m);
        Assert.Equal(expectedTotal, getCartResult.Data.TotalPrice);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Task 8.4: Test error scenarios with proper rollback
    // **Validates: Requirements 2.1, 2.4**
    // ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ErrorScenario_DatabaseTimeoutDuringRegistration_RollsBackProperly()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var customerDto = new CustomerDto
        {
            Email = "timeout@example.com",
            Password = "SecurePass@123",
            FullName = "Timeout Test",
            PhoneNumber = "1111111111",
            DateOfBirth = new DateTime(1992, 7, 10),
            Address = "789 Timeout Rd"
        };

        // Setup UserManager mock
        var userStore = new Mock<IUserStore<AppUser>>();
        var mockUserManager = new Mock<UserManager<AppUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        mockUserManager.Setup(um => um.FindByEmailAsync(customerDto.Email))
            .ReturnsAsync((AppUser)null);
        mockUserManager.Setup(um => um.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<AppUser>(), "Customer"))
            .ReturnsAsync(IdentityResult.Success);

        // Setup SignInManager mock
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        var mockSignInManager = new Mock<SignInManager<AppUser>>(
            mockUserManager.Object,
            contextAccessor.Object,
            userPrincipalFactory.Object,
            null, null, null, null);

        // Setup other service mocks
        var mockJwtService = new Mock<IJwtService>();
        var mockEmailService = new Mock<IEmailService>();
        var mockFileService = new Mock<IFileService>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<AuthService>>();

        // Setup UnitOfWork to simulate database timeout
        mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        
        // Customer repository throws timeout exception
        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();
        mockCustomerRepo.Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .ThrowsAsync(new TimeoutException("Database timeout"));
        mockUnitOfWork.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);

        var authService = new AuthService(
            mockUserManager.Object,
            mockSignInManager.Object,
            mockJwtService.Object,
            mockEmailService.Object,
            mockFileService.Object,
            mockUnitOfWork.Object,
            mockLogger.Object
        );

        // ── Act ──────────────────────────────────────────────────────────────
        var result = await authService.RegisterCustomerAsync(customerDto);

        // ── Assert ───────────────────────────────────────────────────────────
        // Verify registration failed
        Assert.False(result.IsSuccess, "Registration should fail due to database timeout");
        Assert.NotEmpty(result.Errors);
        
        // Verify transaction was rolled back
        mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task ErrorScenario_NetworkFailureDuringCustomerCreation_RollsBackAndCleansUp()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var mockFile = new Mock<IFormFile>();
        mockFile.Setup(f => f.Length).Returns(2048);
        mockFile.Setup(f => f.FileName).Returns("profile.jpg");

        var customerDto = new CustomerDto
        {
            Email = "network@example.com",
            Password = "SecurePass@123",
            FullName = "Network Test",
            PhoneNumber = "2222222222",
            DateOfBirth = new DateTime(1988, 11, 25),
            Address = "321 Network Blvd",
            ProfilePicture = mockFile.Object
        };

        // Setup UserManager mock
        var userStore = new Mock<IUserStore<AppUser>>();
        var mockUserManager = new Mock<UserManager<AppUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        mockUserManager.Setup(um => um.FindByEmailAsync(customerDto.Email))
            .ReturnsAsync((AppUser)null);
        mockUserManager.Setup(um => um.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        mockUserManager.Setup(um => um.AddToRoleAsync(It.IsAny<AppUser>(), "Customer"))
            .ReturnsAsync(IdentityResult.Success);

        // Setup SignInManager mock
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        var mockSignInManager = new Mock<SignInManager<AppUser>>(
            mockUserManager.Object,
            contextAccessor.Object,
            userPrincipalFactory.Object,
            null, null, null, null);

        // Setup other service mocks
        var mockJwtService = new Mock<IJwtService>();
        var mockEmailService = new Mock<IEmailService>();
        var mockFileService = new Mock<IFileService>();
        mockFileService.Setup(fs => fs.SaveFileAsync(It.IsAny<IFormFile>(), It.IsAny<string>()))
            .ReturnsAsync("ProfilePictures/profile.jpg");

        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<AuthService>>();

        mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        
        // Customer repository throws network exception
        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();
        mockCustomerRepo.Setup(r => r.AddAsync(It.IsAny<Customer>()))
            .ThrowsAsync(new System.Net.Http.HttpRequestException("Network failure"));
        mockUnitOfWork.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);

        var authService = new AuthService(
            mockUserManager.Object,
            mockSignInManager.Object,
            mockJwtService.Object,
            mockEmailService.Object,
            mockFileService.Object,
            mockUnitOfWork.Object,
            mockLogger.Object
        );

        // ── Act ──────────────────────────────────────────────────────────────
        var result = await authService.RegisterCustomerAsync(customerDto);

        // ── Assert ───────────────────────────────────────────────────────────
        // Verify registration failed
        Assert.False(result.IsSuccess, "Registration should fail due to network failure");
        
        // Verify transaction was rolled back
        mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        
        // Verify profile picture was deleted after rollback
        mockFileService.Verify(fs => fs.DeleteFile("ProfilePictures/profile.jpg"), Times.Once);
    }

    [Fact]
    public async Task ErrorScenario_AppUserCreationFails_DoesNotCreateCustomerOrCommitTransaction()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        var customerDto = new CustomerDto
        {
            Email = "appuserfail@example.com",
            Password = "Weak",  // Intentionally weak password
            FullName = "AppUser Fail Test",
            PhoneNumber = "3333333333",
            DateOfBirth = new DateTime(1995, 2, 14),
            Address = "654 Fail St"
        };

        // Setup UserManager mock
        var userStore = new Mock<IUserStore<AppUser>>();
        var mockUserManager = new Mock<UserManager<AppUser>>(
            userStore.Object, null, null, null, null, null, null, null, null);

        mockUserManager.Setup(um => um.FindByEmailAsync(customerDto.Email))
            .ReturnsAsync((AppUser)null);
        
        // AppUser creation fails due to password policy
        var errors = new List<IdentityError>
        {
            new IdentityError { Code = "PasswordTooShort", Description = "Password must be at least 8 characters" },
            new IdentityError { Code = "PasswordRequiresDigit", Description = "Password must contain a digit" }
        };
        mockUserManager.Setup(um => um.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Failed(errors.ToArray()));

        // Setup SignInManager mock
        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<AppUser>>();
        var mockSignInManager = new Mock<SignInManager<AppUser>>(
            mockUserManager.Object,
            contextAccessor.Object,
            userPrincipalFactory.Object,
            null, null, null, null);

        // Setup other service mocks
        var mockJwtService = new Mock<IJwtService>();
        var mockEmailService = new Mock<IEmailService>();
        var mockFileService = new Mock<IFileService>();
        var mockUnitOfWork = new Mock<IUnitOfWork>();
        var mockLogger = new Mock<ILogger<AuthService>>();

        mockUnitOfWork.Setup(u => u.BeginTransactionAsync()).Returns(Task.CompletedTask);
        mockUnitOfWork.Setup(u => u.RollbackTransactionAsync()).Returns(Task.CompletedTask);
        
        var mockCustomerRepo = new Mock<IGenericRepository<Customer>>();
        mockUnitOfWork.Setup(u => u.Customers).Returns(mockCustomerRepo.Object);

        var authService = new AuthService(
            mockUserManager.Object,
            mockSignInManager.Object,
            mockJwtService.Object,
            mockEmailService.Object,
            mockFileService.Object,
            mockUnitOfWork.Object,
            mockLogger.Object
        );

        // ── Act ──────────────────────────────────────────────────────────────
        var result = await authService.RegisterCustomerAsync(customerDto);

        // ── Assert ───────────────────────────────────────────────────────────
        // Verify registration failed
        Assert.False(result.IsSuccess, "Registration should fail due to AppUser creation failure");
        Assert.Contains(result.Errors, e => e.Contains("Password must be at least 8 characters"));
        
        // Verify Customer was never created
        mockCustomerRepo.Verify(r => r.AddAsync(It.IsAny<Customer>()), Times.Never);
        
        // Verify transaction was rolled back
        mockUnitOfWork.Verify(u => u.RollbackTransactionAsync(), Times.Once);
        mockUnitOfWork.Verify(u => u.CommitTransactionAsync(), Times.Never);
    }
}
