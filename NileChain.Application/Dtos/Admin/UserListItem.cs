namespace NileChain.Application.Dtos.Admin
{
    public class UserListItem
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public string Role { get; set; } = string.Empty;
        public bool IsVerified { get; set; }
        public bool IsBlocked { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public Guid? FarmId { get; set; }
        public Guid? FactoryId { get; set; }
        public string? FarmName { get; set; }
        public string? FactoryName { get; set; }
        public string KybReviewStatus { get; set; } = "Pending";
        public string? KybAdminNote { get; set; }
        public int? LastTrustScore { get; set; }
        public string? LastRecommendation { get; set; }
        public string? PlanCode { get; set; }
        public string? SubscriptionStatus { get; set; }
        public DateTime? SubscriptionPeriodEnd { get; set; }
    }
}
