using Application.Services;
using Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.Extensions
{
    public static class CartServiceExtension
    {
        public static IServiceCollection AddCartService(this IServiceCollection services)
        {
            services.AddScoped<ICartService, CartService>();
            return services;
        }
    }
}
