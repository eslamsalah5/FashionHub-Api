using Domain.Entities;

namespace Application.Services.Interfaces
{
    public interface IJwtService
    {
        Task<string> GenerateTokenAsync(AppUser user);
    }
}