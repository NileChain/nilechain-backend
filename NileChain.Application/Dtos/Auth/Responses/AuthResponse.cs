namespace NileChain.Application.Dtos.Auth.Responses
{
    public class AuthResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public DateTime ExpiresAt { get; set; }

        public UserResponse User { get; set; } = null!;
    }
}
