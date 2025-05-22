using Domain.Enums;

namespace Application.DTOs.Auth
{
    public class AuthResponseDto
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public UserType UserType { get; set; }
        public string Token { get; set; } = string.Empty;
    }
}