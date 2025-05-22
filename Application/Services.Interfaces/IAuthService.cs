using Application.DTOs.Auth;
using Application.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Application.Services.Interfaces
{
    public interface IAuthService
    {
        // Common methods
        Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto loginDto);
        Task<ServiceResult> ForgotPasswordAsync(string email);
        Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
        Task<ServiceResult> ChangePasswordAsync(ClaimsPrincipal user, ChangePasswordDto changePasswordDto);

        // Registration methods
        Task<ServiceResult<IdentityResult>> RegisterCustomerAsync(CustomerDto customerDto);
        
        // Update methods
        Task<ServiceResult> UpdateAdminAsync(ClaimsPrincipal user, string? adminId, UpdateAdminDto updateAdminDto);
        Task<ServiceResult> UpdateCustomerAsync(ClaimsPrincipal user, UpdateCustomerDto updateCustomerDto);

        // Profile methods
        Task<ServiceResult<AdminProfileDto>> GetAdminByIdAsync(string id);
        Task<ServiceResult<CustomerProfileDto>> GetCustomerByIdAsync(string id);

        Task<ServiceResult<object>> GetMyProfileAsync(ClaimsPrincipal user);

    }
}