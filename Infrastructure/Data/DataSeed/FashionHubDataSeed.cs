using Domain.Entities;
using Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Infrastructure.Data.DataSeed
{
    public class FashionHubDataSeed
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FashionHubDataSeed> _logger;

        public FashionHubDataSeed(
            UserManager<AppUser> userManager,
            RoleManager<IdentityRole> roleManager,
            ApplicationDbContext context,
            IConfiguration configuration,
            ILogger<FashionHubDataSeed> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                _logger.LogInformation("Starting database seeding process...");

                // Get passwords from configuration
                var seedPasswords = _configuration.GetSection("SeedUserPasswords");
                var adminPassword = seedPasswords["Admin"] ?? "Admin123!";
                var customerPassword = seedPasswords["Customer"] ?? "Customer123!";                // Seed data in sequence
                await SeedRolesAsync();
                await SeedAdminAsync(adminPassword);
                await SeedCustomersAsync(customerPassword);
                await SeedProductsAsync();
                await SeedCartsAsync();
                await SeedOrdersAsync();

                //await SeedBookingsAndTicketsAsync();

                _logger.LogInformation("Database seeding completed successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during database seeding process");
                throw;
            }
        }

        private async Task SeedRolesAsync()
        {
            _logger.LogInformation("Seeding roles...");

            var roles = new[] { "Admin", "Customer" };

            foreach (var role in roles)
            {
                if (!await _roleManager.RoleExistsAsync(role))
                {
                    _logger.LogInformation("Creating role: {RoleName}", role);
                    await _roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }        private async Task SeedAdminAsync(string password)
        {
            _logger.LogInformation("Seeding Admin...");

            if (await _userManager.FindByNameAsync("Admin") == null)
            {
                _logger.LogInformation("Creating Admin user");

                var admin = new AppUser
                {
                    UserName = "Admin",
                    Email = "eslamsalah5346@gmail.com",
                    EmailConfirmed = true,
                    PhoneNumber = "+201013114472",
                    FullName = "Eslam Salah",
                    Address = "Sharqia, Egypt",
                    DateOfBirth = new DateTime(2001, 11, 11),
                    UserType = UserType.Admin,
                    DateCreated = DateTime.UtcNow
                };

                var result = await _userManager.CreateAsync(admin, password);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Admin user created successfully");
                    await _userManager.AddToRoleAsync(admin, "Admin");
                    _logger.LogInformation("Admin user assigned to Admin role");
                    
                    // Create Admin entity and add to Admin table
                    var adminEntity = new Admin
                    {
                        Id = admin.Id,
                        AppUser = admin,
                        HireDate = DateTime.UtcNow
                    };

                    await _context.Set<Admin>().AddAsync(adminEntity);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Admin entity added to Admin table successfully");
                }
                else
                {
                    var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                    _logger.LogWarning("Failed to create Admin user: {Errors}", errors);
                }
            }
        }
        
        private async Task SeedCustomersAsync(string password)
        {
            _logger.LogInformation("Seeding customers...");
            
            if (!await _context.Customers.AnyAsync())
            {
                _logger.LogInformation("Creating 5 customers");
                
                var customerCredentials = new List<(string username, string email, string fullName, string phone, DateTime dob, string address)>
                {
                    ("sara_customer", "sara@example.com", "Sara Ahmed", "+20123456814", new DateTime(1995, 4, 12), "25 El-Tahrir Street, Cairo"),
                    ("mohamed_customer", "mohamed.p@example.com", "Mohamed Ali", "+20123456815", new DateTime(1992, 7, 8), "10 El-Horreya Road, Alexandria"),
                    ("nada_customer", "nada@example.com", "Nada Mahmoud", "+20123456816", new DateTime(1996, 9, 15), "7 El-Gomhoria Street, Mansoura"),
                    ("adel_customer", "adel@example.com", "Adel Hussein", "+20123456817", new DateTime(1990, 11, 22), "15 Luxor-Aswan Road, Luxor"),
                    ("reem_customer", "reem@example.com", "Reem Tarek", "+20123456818", new DateTime(1993, 2, 18), "20 El-Nasr Street, Hurghada")
                };

                foreach (var cred in customerCredentials)
                {
                    if (await _userManager.FindByNameAsync(cred.username) == null)
                    {
                        _logger.LogInformation("Creating customer user: {Username}", cred.username);
                        
                        var customer = new AppUser
                        {
                            UserName = cred.username,
                            Email = cred.email,
                            EmailConfirmed = true,
                            PhoneNumber = cred.phone,
                            FullName = cred.fullName,
                            Address = cred.address,
                            DateOfBirth = cred.dob,
                            UserType = UserType.Customer,
                            DateCreated = DateTime.UtcNow.AddMonths(-3)
                        };

                        var result = await _userManager.CreateAsync(customer, password);
                        if (result.Succeeded)
                        {
                            await _userManager.AddToRoleAsync(customer, "Customer");

                            // Create Customer entity
                            var customerEntity = new Customer
                            {
                                Id = customer.Id,
                                AppUser = customer
                            };

                            await _context.Customers.AddAsync(customerEntity);
                            await _context.SaveChangesAsync();
                            
                            _logger.LogInformation("Customer {Username} created successfully", cred.username);
                        }
                        else
                        {
                            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                            _logger.LogWarning("Failed to create customer {Username}: {Errors}", cred.username, errors);
                        }
                    }
                }
            }
            else
            {
                _logger.LogInformation("Customers already exist, skipping customer seed");
            }
        }        private async Task SeedProductsAsync()
        {
            _logger.LogInformation("Seeding products...");
            
            if (!await _context.Set<Product>().AnyAsync())
            {
                _logger.LogInformation("Adding products from JSON file");
                  try
                {
                    // Log available product categories for debugging
                    _logger.LogInformation("Available ProductCategory values: {Categories}", 
                        string.Join(", ", Enum.GetNames(typeof(ProductCategory))));
                    
                    // Define the path to the products.json file
                    string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Infrastructure", "Data", "DataSeed", "products.json");
                    
                    // If the file doesn't exist at the expected path, try looking in alternative locations
                    if (!File.Exists(filePath))
                    {
                        // Try alternative paths
                        string[] possiblePaths = new[]
                        {
                            "products.json",
                            Path.Combine("Data", "DataSeed", "products.json"),
                            Path.Combine("Infrastructure", "Data", "DataSeed", "products.json"),
                            Path.Combine("..", "Infrastructure", "Data", "DataSeed", "products.json"),
                            @"e:\iti files\web api\FashionHub\Infrastructure\Data\DataSeed\products.json"
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            if (File.Exists(path))
                            {
                                filePath = path;
                                break;
                            }
                        }
                    }
                    
                    if (!File.Exists(filePath))
                    {
                        _logger.LogError("Products JSON file not found in any of the expected locations");
                        return;
                    }
                    
                    _logger.LogInformation("Reading products data from file: {FilePath}", filePath);
                    
                    // Read JSON file
                    string jsonData = await File.ReadAllTextAsync(filePath);
                    var productData = JsonSerializer.Deserialize<List<ProductData>>(jsonData, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (productData == null || !productData.Any())
                    {
                        _logger.LogWarning("No product data found in JSON file");
                        return;
                    }
                    
                    _logger.LogInformation("Found {Count} products in JSON file", productData.Count);
                    
                    // Create list to hold all products
                    var products = new List<Product>();
                    
                    // Map product data to domain entities
                    foreach (var item in productData)
                    {                        var product = new Product
                        {
                            Name = item.Name,
                            Description = item.Description,
                            Price = item.Price,
                            Category = Enum.Parse<ProductCategory>(item.Category, true),
                            Brand = item.Brand,
                            MainImageUrl = item.MainImageUrl,
                            AdditionalImageUrls = item.AdditionalImageUrls,
                            StockQuantity = item.StockQuantity,
                            Gender = MapGenderValue(item.Gender),
                            AvailableSizes = item.AvailableSizes,
                            AvailableColors = item.AvailableColors,
                            Tags = item.Tags,
                            IsOnSale = item.IsOnSale,
                            DiscountPrice = item.DiscountPrice,
                            IsFeatured = item.IsFeatured,
                            IsActive = true,
                            DateCreated = DateTime.UtcNow,
                            Slug = item.Name.ToLower().Replace(" ", "-").Replace(",", ""),
                            SKU = $"SKU-{(products.Count + 1):D3}"
                        };
                        
                        products.Add(product);
                    }
                    
                    // Add all products in a single operation
                    await _context.Set<Product>().AddRangeAsync(products);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Successfully added {Count} products to the database", products.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while seeding products");
                    throw; // Re-throw the exception to signal the failure
                }
            }
            else
            {
                _logger.LogInformation("Products already exist in database. Skipping product seeding.");
            }
        }
          // Method to seed Cart data
        private async Task SeedCartsAsync()
        {
            _logger.LogInformation("Seeding carts...");
            
            if (!await _context.Carts.AnyAsync())
            {
                try
                {
                    _logger.LogInformation("Reading cart data from carts.json");
                    
                    // Define the path to the carts.json file
                    string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Infrastructure", "Data", "DataSeed", "carts.json");
                    
                    // If the file doesn't exist at the expected path, try looking in alternative locations
                    if (!File.Exists(jsonFilePath))
                    {
                        // Try alternative paths
                        string[] possiblePaths = new[]
                        {
                            "carts.json",
                            Path.Combine("Data", "DataSeed", "carts.json"),
                            Path.Combine("Infrastructure", "Data", "DataSeed", "carts.json"),
                            Path.Combine("..", "Infrastructure", "Data", "DataSeed", "carts.json"),
                            @"e:\iti files\web api\FashionHub\Infrastructure\Data\DataSeed\carts.json",
                            Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Data", "DataSeed", "carts.json"),
                            Path.Combine(Directory.GetCurrentDirectory(), "carts.json")
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            _logger.LogInformation("Checking path: {Path}", path);
                            if (File.Exists(path))
                            {
                                jsonFilePath = path;
                                _logger.LogInformation("Found carts.json at: {Path}", path);
                                break;
                            }
                        }                    }
                    
                    if (!File.Exists(jsonFilePath))
                    {
                        _logger.LogError("Carts JSON file not found in any of the expected locations");
                        return;
                    }
                    
                    _logger.LogInformation("Reading carts data from file: {FilePath}", jsonFilePath);
                    
                    // Read and deserialize the JSON data
                    string jsonData = await File.ReadAllTextAsync(jsonFilePath);
                    var cartData = JsonSerializer.Deserialize<List<CartData>>(jsonData, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (cartData == null || !cartData.Any())
                    {
                        _logger.LogWarning("No cart data found in JSON file");
                        return;
                    }
                    
                    _logger.LogInformation("Found {Count} carts in JSON file", cartData.Count);
                    
                    // Create list to hold all carts
                    var carts = new List<Cart>();
                    var cartItems = new List<CartItem>();
                    
                    // Get all customer IDs from database to match with seed data
                    var customers = await _context.Customers.ToListAsync();
                    var products = await _context.Products.ToListAsync();
                    
                    if (!customers.Any())
                    {
                        _logger.LogWarning("No customers found in database. Cannot seed carts.");
                        return;
                    }
                      // Map cart data to domain entities
                    for (int i = 0; i < Math.Min(cartData.Count, customers.Count); i++)
                    {
                        var cartDataItem = cartData[i];
                        var customerId = string.IsNullOrEmpty(cartDataItem.CustomerId) ? customers[i].Id : cartDataItem.CustomerId;
                        
                        // Verify customer exists
                        if (!string.IsNullOrEmpty(customerId) && !await _context.Customers.AnyAsync(c => c.Id == customerId))
                        {
                            _logger.LogWarning("Customer with ID {CustomerId} not found. Using available customer.", customerId);
                            customerId = customers[i].Id;
                        }
                        
                        var cart = new Cart
                        {
                            CustomerId = customerId,
                            CreatedAt = DateTime.UtcNow,
                            ModifiedAt = DateTime.UtcNow
                        };
                        
                        carts.Add(cart);
                        
                        // We need to save the carts first to get their IDs
                        await _context.Carts.AddAsync(cart);
                    }
                    
                    // Save carts to get their IDs
                    await _context.SaveChangesAsync();
                    
                    // Now add cart items
                    for (int i = 0; i < carts.Count; i++)
                    {
                        var cart = carts[i];
                        var cartDataItem = cartData[i];
                        
                        foreach (var itemData in cartDataItem.CartItems)
                        {
                            // Make sure product exists
                            var productId = itemData.ProductId;
                            if (productId <= 0 || productId > products.Count)
                            {
                                _logger.LogWarning("Product with ID {ProductId} not found. Skipping cart item.", productId);
                                continue;
                            }
                            
                            var cartItem = new CartItem
                            {
                                CartId = cart.Id,
                                ProductId = productId,
                                Quantity = itemData.Quantity,
                                PriceAtAddition = itemData.PriceAtAddition
                            };
                            
                            cartItems.Add(cartItem);
                        }
                    }
                    
                    // Add all cart items in a single operation
                    if (cartItems.Any())
                    {
                        await _context.CartItems.AddRangeAsync(cartItems);
                        await _context.SaveChangesAsync();
                    }
                    
                    _logger.LogInformation("Successfully added {CartCount} carts with {ItemCount} cart items to the database", 
                        carts.Count, cartItems.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while seeding carts");
                    throw; // Re-throw the exception to signal the failure
                }
            }
            else
            {
                _logger.LogInformation("Carts already exist in database. Skipping cart seeding.");
            }
        }
        
        // Helper method to map gender values from JSON to Gender enum
        private Gender MapGenderValue(string gender)
        {
            return gender.ToLower() switch
            {
                "female" => Gender.Women,
                "male" => Gender.Men,
                "unisex" => Gender.Unisex,
                "kids" => Gender.Kids,
                "baby" => Gender.Baby,
                _ => Gender.Unisex // Default to Unisex for unknown values
            };
        }
        
        // Helper class to deserialize product data from JSON
        private class ProductData
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public decimal Price { get; set; }
            public decimal? DiscountPrice { get; set; }
            public bool IsOnSale { get; set; }
            public string Category { get; set; } = string.Empty;
            public string Brand { get; set; } = string.Empty;
            public string MainImageUrl { get; set; } = string.Empty;
            public string AdditionalImageUrls { get; set; } = string.Empty;
            public int StockQuantity { get; set; }
            public string AvailableSizes { get; set; } = string.Empty;
            public string AvailableColors { get; set; } = string.Empty;
            public bool IsFeatured { get; set; }
            public string Gender { get; set; } = string.Empty;
            public string Tags { get; set; } = string.Empty;
        }
        
        // Helper class to deserialize cart data from JSON
        private class CartData
        {
            public string CustomerId { get; set; } = string.Empty;
            public List<CartItemData> CartItems { get; set; } = new List<CartItemData>();
        }
          private class CartItemData
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal PriceAtAddition { get; set; }
        }

        // Method to seed Order data
        private async Task SeedOrdersAsync()
        {
            _logger.LogInformation("Seeding orders...");
            
            if (!await _context.Orders.AnyAsync())
            {
                try
                {
                    _logger.LogInformation("Reading order data from orders.json");
                    
                    // Define the path to the orders.json file
                    string jsonFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Infrastructure", "Data", "DataSeed", "orders.json");
                    
                    // If the file doesn't exist at the expected path, try looking in alternative locations
                    if (!File.Exists(jsonFilePath))
                    {
                        // Try alternative paths
                        string[] possiblePaths = new[]
                        {
                            "orders.json",
                            Path.Combine("Data", "DataSeed", "orders.json"),
                            Path.Combine("Infrastructure", "Data", "DataSeed", "orders.json"),
                            Path.Combine("..", "Infrastructure", "Data", "DataSeed", "orders.json"),
                            @"e:\iti files\web api\FashionHub\Infrastructure\Data\DataSeed\orders.json",
                            Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Data", "DataSeed", "orders.json"),
                            Path.Combine(Directory.GetCurrentDirectory(), "orders.json")
                        };
                        
                        foreach (var path in possiblePaths)
                        {
                            _logger.LogInformation("Checking path: {Path}", path);
                            if (File.Exists(path))
                            {
                                jsonFilePath = path;
                                _logger.LogInformation("Found orders.json at: {Path}", path);
                                break;
                            }
                        }
                    }
                    
                    if (!File.Exists(jsonFilePath))
                    {
                        _logger.LogError("Orders JSON file not found in any of the expected locations");
                        return;
                    }
                    
                    _logger.LogInformation("Reading orders data from file: {FilePath}", jsonFilePath);
                    
                    // Read and deserialize the JSON data
                    string jsonData = await File.ReadAllTextAsync(jsonFilePath);
                    var orderData = JsonSerializer.Deserialize<List<OrderData>>(jsonData, 
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    
                    if (orderData == null || !orderData.Any())
                    {
                        _logger.LogWarning("No order data found in JSON file");
                        return;
                    }
                    
                    _logger.LogInformation("Found {Count} orders in JSON file", orderData.Count);
                      // Create list to hold all orders
                    var orders = new List<Order>();
                    var orderItems = new List<OrderItem>();
                      // Get all customer IDs from database to match with seed data
                    var customers = await _context.Customers.Include(c => c.AppUser).ToListAsync();
                    var products = await _context.Products.ToListAsync();
                    
                    if (!customers.Any())
                    {
                        _logger.LogWarning("No customers found in database. Cannot seed orders.");
                        return;
                    }
                    
                    // Map order data to domain entities
                    foreach (var orderDataItem in orderData)
                    {
                        // Find customer by name
                        var customer = customers.FirstOrDefault(c => c.AppUser.FullName == orderDataItem.CustomerName);
                        if (customer == null)
                        {
                            _logger.LogWarning("Customer with name {CustomerName} not found. Using first available customer.", orderDataItem.CustomerName);
                            customer = customers.First();
                        }
                        
                        var order = new Order
                        {
                            CustomerId = customer.Id,
                            OrderDate = orderDataItem.OrderDate,
                            Status = (OrderStatus)orderDataItem.Status,
                            TotalAmount = orderDataItem.TotalAmount,
                            OrderNotes = orderDataItem.OrderNotes
                        };
                        
                        orders.Add(order);
                    }
                    
                    // Add all orders first to get their IDs
                    await _context.Orders.AddRangeAsync(orders);
                    await _context.SaveChangesAsync();
                      // Now add order items
                    for (int i = 0; i < orders.Count; i++)
                    {
                        var order = orders[i];
                        var orderDataItem = orderData[i];
                        
                        // Add order items
                        foreach (var itemData in orderDataItem.OrderItems)
                        {
                            // Make sure product exists
                            var productId = itemData.ProductId;
                            if (productId <= 0 || !products.Any(p => p.Id == productId))
                            {
                                _logger.LogWarning("Product with ID {ProductId} not found. Skipping order item.", productId);
                                continue;
                            }
                            
                            var orderItem = new OrderItem
                            {
                                OrderId = order.Id,
                                ProductId = productId,
                                ProductName = itemData.ProductName,
                                UnitPrice = itemData.UnitPrice,
                                Quantity = itemData.Quantity,
                                Subtotal = itemData.Subtotal,
                                ProductSKU = itemData.ProductSKU,
                                SelectedSize = itemData.SelectedSize,
                                SelectedColor = itemData.SelectedColor
                            };
                            
                            orderItems.Add(orderItem);
                        }
                    }
                      // Add all order items in single operation
                    if (orderItems.Any())
                    {
                        await _context.OrderItems.AddRangeAsync(orderItems);
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Successfully added {OrderCount} orders with {ItemCount} order items to the database", 
                        orders.Count, orderItems.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while seeding orders");
                    throw; // Re-throw the exception to signal the failure
                }
            }
            else
            {
                _logger.LogInformation("Orders already exist in database. Skipping order seeding.");
            }
        }        // Helper class to deserialize order data from JSON
        private class OrderData
        {
            public string CustomerName { get; set; } = string.Empty;
            public DateTime OrderDate { get; set; }
            public int Status { get; set; }
            public decimal TotalAmount { get; set; }
            public string OrderNotes { get; set; } = string.Empty;
            public List<OrderItemData> OrderItems { get; set; } = new List<OrderItemData>();
        }
        
        private class OrderItemData
        {
            public int ProductId { get; set; }
            public string ProductName { get; set; } = string.Empty;
            public decimal UnitPrice { get; set; }
            public int Quantity { get; set; }
            public decimal Subtotal { get; set; }
            public string ProductSKU { get; set; } = string.Empty;
            public string SelectedSize { get; set; } = string.Empty;
            public string SelectedColor { get; set; } = string.Empty;
        }
    }
}