using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace Application.DTOs.Auth
{
    public class UpdateAdminDto
    {
        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;
        
        [Phone]
        public string PhoneNumber { get; set; } = string.Empty;
        
        public IFormFile? ProfilePicture { get; set; }
        
        public DateTime? DateOfBirth { get; set; }
        
        public string Address { get; set; } = string.Empty;
    }
}