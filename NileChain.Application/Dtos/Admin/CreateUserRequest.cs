namespace NileChain.Application.Dtos.Admin
{
    public class CreateUserRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Governorate { get; set; }
        public decimal? SizeInFeddans { get; set; }
    }
}
