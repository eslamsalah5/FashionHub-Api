using Application.DTOs.Auth;
using Application.Models;
using Application.Services.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Repositories.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text;

namespace Application.Services.Auth
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IJwtService _jwtService;
        private readonly IEmailService _emailService;
        private readonly IFileService _fileService;
        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IJwtService jwtService,
            IEmailService emailService,
            IFileService fileService,
            IUnitOfWork unitOfWork,
            ILogger<AuthService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtService = jwtService;
            _emailService = emailService;
            _fileService = fileService;
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        #region Common Authentication Methods

        public async Task<ServiceResult<AuthResponseDto>> LoginAsync(LoginDto loginDto)
        {
            // Try to find the user by email or username
            var user = await _userManager.FindByEmailAsync(loginDto.EmailOrUsername);
            
            // If not found by email, try to find by username
            if (user == null)
            {
                user = await _userManager.FindByNameAsync(loginDto.EmailOrUsername);
            }
            
            if (user == null)
            {
                return ServiceResult<AuthResponseDto>.Failure("Invalid login attempt.");
            }

            // Check the password
            var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, false);
            
            if (!result.Succeeded)
            {
                return ServiceResult<AuthResponseDto>.Failure("Invalid login attempt.");
            }

            // Update last login time
            user.LastLogin = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            // Generate JWT token
            var token = await _jwtService.GenerateTokenAsync(user);
            
            // Create response with user information
            var authResponse = new AuthResponseDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                UserName = user.UserName ?? string.Empty,
                FullName = user.FullName,
                UserType = user.UserType,
                Token = token
            };
            
            return ServiceResult<AuthResponseDto>.Success(authResponse);
        }

        public async Task<ServiceResult> ForgotPasswordAsync(string email)
        {
            var user = await _userManager.FindByEmailAsync(email);
            
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return ServiceResult.Success();
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            await _emailService.SendPasswordResetEmailAsync(email, user.UserName ?? email, token);
            
            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
        {
            var user = await _userManager.FindByEmailAsync(resetPasswordDto.Email);
            
            if (user == null)
            {
                // Don't reveal that the user does not exist
                return ServiceResult.Failure("Password reset failed.");
            }

            var resetResult = await _userManager.ResetPasswordAsync(
                user, 
                resetPasswordDto.Token, 
                resetPasswordDto.NewPassword);

            if (!resetResult.Succeeded)
            {
                var errors = resetResult.Errors.Select(e => e.Description);
                return ServiceResult.Failure(errors);
            }

            return ServiceResult.Success();
        }

        public async Task<ServiceResult> ChangePasswordAsync(ClaimsPrincipal user, ChangePasswordDto changePasswordDto)
        {
            if (user == null || !user.Identity.IsAuthenticated)
            {
                return ServiceResult.Failure("Authentication required.");
            }

            // Get the user ID from ClaimsPrincipal
            var userId = _userManager.GetUserId(user);
            
            if (string.IsNullOrEmpty(userId))
            {
                return ServiceResult.Failure("User ID not found in authentication token.");
            }

            // Get the user by ID
            var appUser = await _userManager.FindByIdAsync(userId);
            
            if (appUser == null)
            {
                return ServiceResult.Failure("User not found.");
            }

            // Change the password
            var changePasswordResult = await _userManager.ChangePasswordAsync(
                appUser, 
                changePasswordDto.CurrentPassword, 
                changePasswordDto.NewPassword);

            if (!changePasswordResult.Succeeded)
            {
                var errors = changePasswordResult.Errors.Select(e => e.Description);
                return ServiceResult.Failure(errors);
            }

            // Send notification email
            var email = appUser.Email ?? string.Empty;
            var username = appUser.UserName ?? email;
            await _emailService.SendPasswordChangedNotificationAsync(
                email, 
                username);

            return ServiceResult.Success();
        }

        #endregion

        #region Registration Methods
        
                public async Task<ServiceResult<IdentityResult>> RegisterCustomerAsync(CustomerDto customerDto)
        {
            _logger.LogInformation("Starting customer registration for email: {Email}", customerDto.Email);
            
            // Check if user with email already exists
            var existingUser = await _userManager.FindByEmailAsync(customerDto.Email);
            
            if (existingUser != null)
            {
                _logger.LogWarning("Registration failed: Email {Email} is already taken", customerDto.Email);
                return ServiceResult<IdentityResult>.Failure("Email is already taken.");
            }

            // Save profile picture if provided, otherwise use empty string
            string profilePicPath = string.Empty;
            if (customerDto.ProfilePicture != null && customerDto.ProfilePicture.Length > 0)
            {
                try
                {
                    profilePicPath = await _fileService.SaveFileAsync(customerDto.ProfilePicture, "ProfilePictures");
                    _logger.LogInformation("Profile picture saved successfully at: {Path}", profilePicPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save profile picture for email: {Email}", customerDto.Email);
                    return ServiceResult<IdentityResult>.Failure("Failed to save profile picture. Please try again.");
                }
            }

            // Create AppUser
            var appUser = new AppUser
            {
                UserName = customerDto.Email,
                Email = customerDto.Email,
                FullName = customerDto.FullName,
                PhoneNumber = customerDto.PhoneNumber,
                DateOfBirth = customerDto.DateOfBirth,
                Address = customerDto.Address,
                ProfilePictureUrl = profilePicPath, // Save the path to the profile picture
                UserType = UserType.Customer,
                DateCreated = DateTime.UtcNow,
            };

            // Begin explicit transaction to ensure atomic creation of AppUser and Customer
            await _unitOfWork.BeginTransactionAsync();
            _logger.LogInformation("Transaction started for customer registration: {Email}", customerDto.Email);
            
            try
            {
                // Create user with password
                var result = await _userManager.CreateAsync(appUser, customerDto.Password);

                if (!result.Succeeded)
                {
                    _logger.LogWarning("AppUser creation failed for email: {Email}. Errors: {Errors}", 
                        customerDto.Email, 
                        string.Join(", ", result.Errors.Select(e => e.Description)));
                    
                    // Rollback transaction
                    await _unitOfWork.RollbackTransactionAsync();
                    _logger.LogInformation("Transaction rolled back for email: {Email}", customerDto.Email);
                    
                    // Delete uploaded image if user creation fails
                    if (!string.IsNullOrEmpty(profilePicPath))
                    {
                        _fileService.DeleteFile(profilePicPath);
                        _logger.LogInformation("Profile picture deleted after failed registration: {Path}", profilePicPath);
                    }
                    return ServiceResult<IdentityResult>.Failure(result.Errors.Select(e => e.Description));
                }

                _logger.LogInformation("AppUser created successfully with ID: {UserId}", appUser.Id);

                // Assign role
                await _userManager.AddToRoleAsync(appUser, "Customer");
                _logger.LogInformation("Customer role assigned to user: {UserId}", appUser.Id);

                // Create customer entity with same ID as AppUser
                var customer = new Customer
                {
                    Id = appUser.Id
                };

                // Validate ID consistency (defensive check)
                if (customer.Id != appUser.Id)
                {
                    _logger.LogError("Data integrity error: Customer ID {CustomerId} does not match AppUser ID {AppUserId}", 
                        customer.Id, appUser.Id);
                    
                    await _unitOfWork.RollbackTransactionAsync();
                    _logger.LogInformation("Transaction rolled back due to ID mismatch for email: {Email}", customerDto.Email);
                    
                    // Delete uploaded image
                    if (!string.IsNullOrEmpty(profilePicPath))
                    {
                        _fileService.DeleteFile(profilePicPath);
                        _logger.LogInformation("Profile picture deleted after ID mismatch: {Path}", profilePicPath);
                    }
                    
                    return ServiceResult<IdentityResult>.Failure("Data integrity error: Customer ID does not match AppUser ID. Please contact support.");
                }

                // Add customer to database
                await _unitOfWork.Customers.AddAsync(customer);
                _logger.LogInformation("Customer entity created with ID: {CustomerId}", customer.Id);
                
                // Commit transaction - both AppUser and Customer are created atomically
                await _unitOfWork.CommitTransactionAsync();
                _logger.LogInformation("Transaction committed successfully. Customer registration completed for email: {Email}, UserId: {UserId}", 
                    customerDto.Email, appUser.Id);

                return ServiceResult<IdentityResult>.Success(result);
            }
            catch (DbUpdateException dbEx)
            {
                // Database-specific errors
                _logger.LogError(dbEx, "Database error during customer registration for email: {Email}. Inner exception: {InnerException}", 
                    customerDto.Email, 
                    dbEx.InnerException?.Message ?? "None");
                
                // Rollback transaction on any error
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogInformation("Transaction rolled back after database error for email: {Email}", customerDto.Email);
                
                // Delete uploaded image if transaction fails
                if (!string.IsNullOrEmpty(profilePicPath))
                {
                    _fileService.DeleteFile(profilePicPath);
                    _logger.LogInformation("Profile picture deleted after database error: {Path}", profilePicPath);
                }
                
                return ServiceResult<IdentityResult>.Failure("Registration failed due to a database error. Please try again or contact support if the problem persists.");
            }
            catch (Exception ex)
            {
                // General errors
                _logger.LogError(ex, "Unexpected error during customer registration for email: {Email}. Error type: {ErrorType}, Message: {Message}", 
                    customerDto.Email, 
                    ex.GetType().Name,
                    ex.Message);
                
                // Rollback transaction on any error
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogInformation("Transaction rolled back after unexpected error for email: {Email}", customerDto.Email);
                
                // Delete uploaded image if transaction fails
                if (!string.IsNullOrEmpty(profilePicPath))
                {
                    _fileService.DeleteFile(profilePicPath);
                    _logger.LogInformation("Profile picture deleted after unexpected error: {Path}", profilePicPath);
                }
                
                return ServiceResult<IdentityResult>.Failure($"Registration failed due to an unexpected error. Please try again or contact support if the problem persists. Error: {ex.Message}");
            }
        }


        #endregion

        #region Update Methods
        
        public async Task<ServiceResult> UpdateAdminAsync(ClaimsPrincipal user, string? adminId, UpdateAdminDto updateAdminDto)
        {
            // Authentication validation checks
            if (user == null || !user.Identity.IsAuthenticated)
            {
                return ServiceResult.Failure("Authentication required");
            }

            // Get the current user's ID
            var userId = _userManager.GetUserId(user);
            
            if (userId == null)
            {
                return ServiceResult.Failure("User ID not found in authentication token");
            }

            // Get role claims to validate the user is an Admin
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            if (!roles.Contains("Admin"))
            {
                return ServiceResult.Failure("User is not an Admin");
            }
            
            // Get the user to update
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser == null)
            {
                return ServiceResult.Failure("User not found");
            }
            
            // Update basic user information
            appUser.FullName = updateAdminDto.FullName;
            appUser.PhoneNumber = updateAdminDto.PhoneNumber;
            appUser.DateOfBirth = updateAdminDto.DateOfBirth;
            appUser.Address = updateAdminDto.Address;
            
            
            // Handle profile picture if provided
            if (updateAdminDto.ProfilePicture != null && updateAdminDto.ProfilePicture.Length > 0)
            {
                // Delete old profile picture if it exists
                if (!string.IsNullOrEmpty(appUser.ProfilePictureUrl))
                {
                    _fileService.DeleteFile(appUser.ProfilePictureUrl);
                }

                // Save new profile picture
                string profilePicPath = await _fileService.SaveFileAsync(updateAdminDto.ProfilePicture, "ProfilePictures");
                appUser.ProfilePictureUrl = profilePicPath;
            }
            
            // Save changes to AppUser
            var result = await _userManager.UpdateAsync(appUser);
            
            if (!result.Succeeded)
            {
                return ServiceResult.Failure(result.Errors.Select(e => e.Description));
            }
            
            return ServiceResult.Success();
        }
        
                public async Task<ServiceResult> UpdateCustomerAsync(ClaimsPrincipal user,  UpdateCustomerDto updateCustomerDto)
        {
             // Authentication validation checks
            if (user == null || !user.Identity.IsAuthenticated)
            {
                return ServiceResult.Failure("Authentication required");
            }

            // Get the current user's ID
            var userId = _userManager.GetUserId(user);
            
            if (userId == null)
            {
                return ServiceResult.Failure("User ID not found in authentication token");
            }

            // Get role claims to validate the user is a Passenger
            var roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            if (!roles.Contains("Customer"))
            {
                return ServiceResult.Failure("User is not a Customer");
            }
            
            // Get the user to update
            var appUser = await _userManager.FindByIdAsync(userId);
            if (appUser == null)
            {
                return ServiceResult.Failure("User not found");
            }
            
            // Update basic user information
            appUser.FullName = updateCustomerDto.FullName;
            appUser.PhoneNumber = updateCustomerDto.PhoneNumber;
            appUser.DateOfBirth = updateCustomerDto.DateOfBirth;
            appUser.Address = updateCustomerDto.Address;
            
            
            // Handle profile picture if provided
            if (updateCustomerDto.ProfilePicture != null && updateCustomerDto.ProfilePicture.Length > 0)
            {
                // Delete old profile picture if it exists
                if (!string.IsNullOrEmpty(appUser.ProfilePictureUrl))
                {
                    _fileService.DeleteFile(appUser.ProfilePictureUrl);
                }

                // Save new profile picture
                string profilePicPath = await _fileService.SaveFileAsync(updateCustomerDto.ProfilePicture, "ProfilePictures");
                appUser.ProfilePictureUrl = profilePicPath;
            }
            
            // Save changes to AppUser
            var result = await _userManager.UpdateAsync(appUser);
            
            if (!result.Succeeded)
            {
                return ServiceResult.Failure(result.Errors.Select(e => e.Description));
            }
            
            return ServiceResult.Success();
        }

        #endregion

        #region User Profile
        public async Task<ServiceResult<object>> GetMyProfileAsync(ClaimsPrincipal user)
        {
            // Authentication validation checks
            if (user == null || !user.Identity.IsAuthenticated)
            {
                return ServiceResult<object>.Failure("Authentication required");
            }

            // Get the current user's ID
            var userId = _userManager.GetUserId(user);

            if (string.IsNullOrEmpty(userId))
            {
                return ServiceResult<object>.Failure("User ID not found in authentication token");
            }

            // Get the user to determine the type
            var appUser = await _userManager.FindByIdAsync(userId);

            if (appUser == null)
            {
                return ServiceResult<object>.Failure("User not found");
            }

            // Call the appropriate method based on user type
            switch (appUser.UserType)
            {
                case UserType.Admin:
                    var adminResult = await GetAdminByIdAsync(userId);
                    if (adminResult.IsSuccess)
                        return ServiceResult<object>.Success(adminResult.Data);
                    return ServiceResult<object>.Failure(adminResult.Errors);

                case UserType.Customer:
                    var customerResult = await GetCustomerByIdAsync(userId);
                    if (customerResult.IsSuccess)
                        return ServiceResult<object>.Success(customerResult.Data);
                    return ServiceResult<object>.Failure(customerResult.Errors);

                default:
                    return ServiceResult<object>.Failure($"User type {appUser.UserType} not supported");
            }
        }

        public async Task<ServiceResult<AdminProfileDto>> GetAdminByIdAsync(string id)
        {
            var admin = await _unitOfWork.Users.GetByIdAsync(id);
            
            if (admin == null)
            {
                return ServiceResult<AdminProfileDto>.Failure("Admin not found");
            }

            var adminProfile = new AdminProfileDto
            {
                Id = admin.Id,
                FullName = admin.FullName,
                Email = admin.Email,
                PhoneNumber = admin.PhoneNumber,
                DateOfBirth = admin.DateOfBirth,
                Address = admin.Address,
                ProfilePictureUrl = admin.ProfilePictureUrl,
            };

            return ServiceResult<AdminProfileDto>.Success(adminProfile);
        }
        
        
        public async Task<ServiceResult<CustomerProfileDto>> GetCustomerByIdAsync(string id)
        {
            var customer = await _unitOfWork.Users.GetCustomerByUserIdAsync(id);

            if (customer == null || customer.AppUser == null)
            {
                return ServiceResult<CustomerProfileDto>.Failure("Customer not found");
            }

            var appUser = customer.AppUser;

            var customerProfile = new CustomerProfileDto
            {
                Id = appUser.Id,
                FullName = appUser.FullName,
                Email = appUser.Email,
                PhoneNumber = appUser.PhoneNumber,
                DateOfBirth = appUser.DateOfBirth,
                Address = appUser.Address,
                ProfilePictureUrl = appUser.ProfilePictureUrl,
                DateCreated = appUser.DateCreated,
                LastLogin = appUser.LastLogin,
                UserType = "Customer"
            };

            return ServiceResult<CustomerProfileDto>.Success(customerProfile);
        }

        #endregion

        #region Helper Methods

        private string GenerateRandomPassword()
        {
            const string lowerChars = "abcdefghijklmnopqrstuvwxyz";
            const string upperChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            const string numbers = "0123456789";
            const string specials = "!@#$%^&*()_-+=<>?";

            var random = new Random();
            var password = new StringBuilder();

            // Add at least one of each character type
            password.Append(lowerChars[random.Next(lowerChars.Length)]);
            password.Append(upperChars[random.Next(upperChars.Length)]);
            password.Append(numbers[random.Next(numbers.Length)]);
            password.Append(specials[random.Next(specials.Length)]);

            // Add additional random characters to reach desired length (e.g., 12)
            var allChars = lowerChars + upperChars + numbers + specials;
            for (int i = 0; i < 8; i++) // 8 more characters for a total of 12
            {
                password.Append(allChars[random.Next(allChars.Length)]);
            }

            // Shuffle the password characters
            return new string(password.ToString().ToCharArray().OrderBy(x => random.Next()).ToArray());
        }



        #endregion

    }
}
