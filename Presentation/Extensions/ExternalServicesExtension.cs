using Application.Models;
using Application.Services.Interfaces;
using Infrastructure.ExternalServices;
using Infrastructure.ExternalServices.EmailService;
using Infrastructure.ExternalServices.FileService;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Presentation.Extensions
{
    public static class ExternalServicesExtension
    {
        public static IServiceCollection AddExternalServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure email settings
            services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
            
            // Register the email service
            services.AddScoped<IEmailService, EmailService>();
            
            // Register file service if it exists
            services.AddScoped<IFileService, FileService>();
            
            // Add JWT service registration
            services.AddScoped<IJwtService, JwtService>();
            
            return services;
        }
    }
}
