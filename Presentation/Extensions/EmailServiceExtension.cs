using Application.Models;
using Application.Services.Interfaces;
using Infrastructure.ExternalServices.EmailService;

namespace Presentation.Extensions
{
    public static class EmailServicesExtension
    {
        public static IServiceCollection AddEmailServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure Email Settings
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            
            // Register Services
            services.AddTransient<IEmailService, EmailService>();

            return services;
        }
    }
}