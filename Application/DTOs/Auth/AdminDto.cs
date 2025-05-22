// using Microsoft.AspNetCore.Http;
// using System.ComponentModel.DataAnnotations;

// namespace Application.DTOs.Auth
// {
//     public class AdminDto
//     {
//         [Required]
//         [StringLength(100)]
//         public string FullName { get; set; } = string.Empty;
        
//         [Required]
//         [EmailAddress]
//         public string Email { get; set; } = string.Empty;
        
//         [Required]
//         [Phone]
//         public string PhoneNumber { get; set; } = string.Empty;
        
//         public IFormFile? ProfilePicture { get; set; }
        
//         public DateTime? DateOfBirth { get; set; }
        
//         public string Address { get; set; } = string.Empty;
//     }
// }