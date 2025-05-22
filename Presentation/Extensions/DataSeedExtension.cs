using Infrastructure.Data.DataSeed;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Presentation.Extensions
{
    public static class DataSeedExtension
    {
        public static IServiceCollection AddDataSeedServices(this IServiceCollection services)
        {
            // Register the data seed service
            services.AddScoped<FashionHubDataSeed>();
            
            return services;
        }
        
        public static async Task<WebApplication> SeedDatabaseAsync(this WebApplication app)
        {
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<Program>>();
                
                try
                {
                    logger.LogInformation("Checking database connection...");
                    
                    var seedService = services.GetRequiredService<FashionHubDataSeed>();
                    await seedService.SeedAsync();
                    
                    logger.LogInformation("Database seeding completed successfully!");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred during database seeding process");
                }
            }
            
            return app;
        }
    }
}