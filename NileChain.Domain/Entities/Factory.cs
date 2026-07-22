using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class Factory
{
    public Guid FactoryId { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = default!;
    public string? Location { get; set; }
    public string? Governorate { get; set; }
    public string? IndustryType { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser User { get; set; } = default!;
    public ICollection<SupplyRequest> SupplyRequests { get; set; } = new List<SupplyRequest>();
}
