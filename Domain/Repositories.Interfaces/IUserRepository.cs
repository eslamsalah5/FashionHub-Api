using Domain.Entities;
using System.Security.Claims;

namespace Domain.Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<AppUser?> GetByIdAsync(string userId);
        Task<string?> GetUserIdFromClaimsPrincipalAsync(ClaimsPrincipal user);

        Task<Admin?> GetAdminByUserIdAsync(string userId);
        Task<Customer?> GetCustomerByUserIdAsync(string userId);
        Task<AppUser?> GetUserByEmailAsync(string email);
        Task<bool> CheckPasswordAsync(AppUser user, string password);

    }
}