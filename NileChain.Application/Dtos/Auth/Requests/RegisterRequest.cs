namespace NileChain.Application.Dtos.Auth.Requests
{
    public class RegisterRequest
    {
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string ConfirmPassword { get; set; } = string.Empty;
        public string BusinessType { get; set; } = string.Empty;
        /// <summary>Egyptian mobile: 01XXXXXXXXX (also accepts +20 / 0020 forms).</summary>
        public string Phone { get; set; } = string.Empty;
        public string? Name { get; set; }
        public string? Governorate { get; set; }
        public decimal? SizeInFeddans { get; set; }
    }
}
