namespace NileChain.Application.Dtos.Auth.Responses
{
    public class UserResponse
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string Role { get; set; } = string.Empty;

        public bool EmailConfirmed { get; set; }

        public bool IsVerified { get; set; }

        public string KybReviewStatus { get; set; } = "Pending";

        public string? KybAdminNote { get; set; }
    }
}
