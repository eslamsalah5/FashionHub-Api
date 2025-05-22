using Application.Services;
using Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.Extensions
{
    public static class ProductServiceExtension
    {
        public static IServiceCollection AddProductService(this IServiceCollection services)
        {
            services.AddScoped<IProductService, ProductService>();
            return services;
        }
    }
}
