using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class KybDecision
{
    public Guid DecisionId { get; set; }
    public Guid UserId { get; set; }
    public Guid AdminUserId { get; set; }
    public KybDecisionAction Action { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int TrustScoreAtDecision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
    public ApplicationUser? AdminUser { get; set; }
}
