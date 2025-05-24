using Application.Services;
using Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.Extensions
{
    public static class OrderServiceExtension
    {
        public static IServiceCollection AddOrderService(this IServiceCollection services)
        {
            services.AddScoped<IOrderService, OrderService>();
            
            return services;
        }
    }
}
