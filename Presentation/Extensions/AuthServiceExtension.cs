using Application.Services.Auth;
using Application.Services.Interfaces;

namespace Presentation.Extensions
{
    public static class AuthServiceExtension
    {
        public static IServiceCollection AddAuthService(this IServiceCollection services)
        {
            services.AddScoped<IAuthService, AuthService>();
            return services;
        }
    }
}