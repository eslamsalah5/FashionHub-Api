using Application.DTOs.Auth;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Presentation.Errors;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Presentation.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse>> Login(LoginDto loginDto)
        {
            var result = await _authService.LoginAsync(loginDto);
            if (!result.IsSuccess)
            {
                return Unauthorized(new ApiResponse(401, "Invalid login credentials"));
            }

            return Ok(new ApiResponse(200, "Login successful") { Data = result.Data });
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult<ApiResponse>> ForgotPassword([FromBody] string email)
        {
            var result = await _authService.ForgotPasswordAsync(email);
            
            return Ok(new ApiResponse(200, "If the email exists in our system, a password reset link has been sent"));
        }

        [HttpPost("reset-password")]
        public async Task<ActionResult<ApiResponse>> ResetPassword(ResetPasswordDto resetPasswordDto)
        {
            var result = await _authService.ResetPasswordAsync(resetPasswordDto);

            if (!result.IsSuccess)
            {
                return BadRequest(new ApiValidationErrorResponse 
                { 
                    Errors = result.Errors 
                });
            }

            return Ok(new ApiResponse(200, "Password has been reset successfully"));
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<ActionResult<ApiResponse>> ChangePassword(ChangePasswordDto changePasswordDto)
        {
            var result = await _authService.ChangePasswordAsync(User, changePasswordDto);

            if (!result.IsSuccess)
            {
                return BadRequest(new ApiValidationErrorResponse 
                { 
                    Errors = result.Errors 
                });
            }

            return Ok(new ApiResponse(200, "Password has been changed successfully"));
        }

        [HttpPost("register-customer")]
        public async Task<ActionResult<ApiResponse>> RegisterCustomer(CustomerDto customerDto)
        {
            var result = await _authService.RegisterCustomerAsync(customerDto);

            if (!result.IsSuccess)
            {
                return BadRequest(new ApiValidationErrorResponse 
                { 
                    Errors = result.Errors 
                });
            }

            return StatusCode(201, new ApiResponse(201, "Customer registered successfully"));
        }

        [Authorize(Roles = "Admin")]
        [HttpPut("update-admin")]
        public async Task<ActionResult<ApiResponse>> UpdateAdmin([FromQuery] string? adminId, [FromForm] UpdateAdminDto updateAdminDto)
        {
            var result = await _authService.UpdateAdminAsync(User, adminId, updateAdminDto);

            if (!result.IsSuccess)
            {
                return BadRequest(new ApiValidationErrorResponse 
                { 
                    Errors = result.Errors 
                });
            }

            return Ok(new ApiResponse(200, "Admin updated successfully"));
        }

        [Authorize(Roles = "Customer")]
        [HttpPut("update-customer")]
        public async Task<ActionResult<ApiResponse>> UpdateCustomer([FromForm] UpdateCustomerDto updateCustomerDto)
        {
            var result = await _authService.UpdateCustomerAsync(User, updateCustomerDto);

            if (!result.IsSuccess)
            {
                return BadRequest(new ApiValidationErrorResponse 
                { 
                    Errors = result.Errors 
                });
            }

            return Ok(new ApiResponse(200, "Customer updated successfully"));
        }

        //[Authorize(Roles = "Admin,SuperAdmin")]
        [HttpGet("admin/{id}")]
        public async Task<ActionResult<AdminProfileDto>> GetAdminById(string id)
        {
            var result = await _authService.GetAdminByIdAsync(id);
            
            if (!result.IsSuccess)
            {
                return NotFound(new ApiResponse(404, result.Errors.FirstOrDefault() ?? "Admin not found"));
            }
            
            return Ok(result.Data);
        }

        //[Authorize(Roles = "Customer,Admin,SuperAdmin")]
        [HttpGet("customer/{id}")]
        public async Task<ActionResult<CustomerProfileDto>> GetCustomerById(string id)
        {
            var result = await _authService.GetCustomerByIdAsync(id);
            
            if (!result.IsSuccess)
            {
                return NotFound(new ApiResponse(404, result.Errors.FirstOrDefault() ?? "Customer not found"));
            }
            
            return Ok(result.Data);
        }

        [Authorize]
        [HttpGet("my-profile")]
        public async Task<ActionResult<object>> GetMyProfile()
        {
            var result = await _authService.GetMyProfileAsync(User);
            
            if (!result.IsSuccess)
            {
                return BadRequest(new ApiResponse(400, result.Errors.FirstOrDefault() ?? "Failed to retrieve profile"));
            }
            
            return Ok(result.Data);
        }
    }
}