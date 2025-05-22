using Application.Models;
using Application.Services.Interfaces;
using Infrastructure.ExternalServices;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Text;

namespace Presentation.Extensions
{
    public static class JwtServiceExtension
    {
        public static IServiceCollection AddJwtServices(this IServiceCollection services, IConfiguration configuration)
        {
            // Configure JWT Settings
            services.Configure<JwtSettings>(configuration.GetSection("JWT"));
            
            // Register JWT Service
            services.AddScoped<IJwtService, JwtService>();

            // Configure JWT Authentication
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["JWT:Issuer"],
                    ValidAudience = configuration["JWT:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(configuration["JWT:Key"] ?? throw new InvalidOperationException("JWT:Key is not configured")))
                };
            });

            return services;
        }
    }
}