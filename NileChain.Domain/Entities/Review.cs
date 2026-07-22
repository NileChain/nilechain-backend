using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class Review
{
    public Guid ReviewId { get; set; }
    public Guid ContractId { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid TargetId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
    public ApplicationUser Reviewer { get; set; } = default!;
    public ApplicationUser Target { get; set; } = default!;
}
