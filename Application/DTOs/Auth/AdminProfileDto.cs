using System;

namespace Application.DTOs.Auth
{
    public class AdminProfileDto
    {
        public string Id { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public string? ProfilePictureUrl { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Address { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public DateTime? LastLogin { get; set; }
        public string UerType { get; set; } = "Admin";
    }
}