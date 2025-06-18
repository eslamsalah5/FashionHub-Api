using Application.Services;
using Application.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Presentation.Extensions
{
    public static class PaymentServiceExtension
    {
        public static IServiceCollection AddPaymentService(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure Stripe
            StripeConfiguration.ApiKey = configuration["Stripe:SecretKey"];
            
            services.AddScoped<IPaymentService, PaymentService>();
            
            return services;
        }
    }
}
