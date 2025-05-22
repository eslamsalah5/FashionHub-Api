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
                var customerPassword = seedPasswords["Customer"] ?? "Customer123!";

                // Seed data in sequence
                await SeedRolesAsync();
                await SeedAdminAsync(adminPassword);
                await SeedCustomersAsync(customerPassword);
                await SeedProductsAsync();
 
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
                    {
                        var product = new Product
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
                            Slug = item.Name.ToLower().Replace(" ", "-"),
                            SKU = $"SKU-{Guid.NewGuid().ToString().Substring(0, 8).ToUpper()}"
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
    }
}