using Application.Services.Interfaces;
using Infrastructure.ExternalServices.FileService;

namespace Presentation.Extensions
{
    public static class FileServiceExtension
    {
        public static IServiceCollection AddFileService(this IServiceCollection services)
        {
            services.AddScoped<IFileService, FileService>();
            return services;
        }
    }
}