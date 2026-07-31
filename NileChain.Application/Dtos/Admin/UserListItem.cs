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
        public string? FarmName { get; set; }
        public string? FactoryName { get; set; }
    }
}
