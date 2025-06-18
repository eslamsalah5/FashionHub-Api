using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Domain.Entities;
using Domain.Repositories.Interfaces;
using System.Security.Claims;
using System.Threading.Tasks;
using Infrastructure.Data;
using Domain.Enums;

namespace Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;

        public UserRepository(
            ApplicationDbContext context,
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager)
        {
            _context = context;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public async Task<AppUser?> GetByIdAsync(string userId)
        {
            return await _userManager.FindByIdAsync(userId);
        }
        
        public Task<string?> GetUserIdFromClaimsPrincipalAsync(ClaimsPrincipal user)
        {
            // Since _userManager.GetUserId is not an async method, we wrap it in a Task.FromResult
            return Task.FromResult(_userManager.GetUserId(user));
        }

        public async Task<Admin?> GetAdminByUserIdAsync(string userId)
        {
            return await _context.Admins
                .FirstOrDefaultAsync(a => a.Id == userId);
        }        public async Task<Customer?> GetCustomerByUserIdAsync(string userId)
        {
            // Add logging or more detailed error handling
            var customer = await _context.Customers
                .Include(c => c.AppUser)
                .FirstOrDefaultAsync(c => c.Id == userId);
            
            return customer;
        }

        public async Task<AppUser?> GetUserByEmailAsync(string email)
        {
            return await _userManager.FindByEmailAsync(email);
        }

        public async Task<bool> CheckPasswordAsync(AppUser user, string password)
        {
            return await _userManager.CheckPasswordAsync(user, password);
        }
    }
}