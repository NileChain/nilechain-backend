using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

/// <summary>
/// Mid-deal operational dispute/claim on a signed contract (not legal arbitration).
/// </summary>
public class Dispute
{
    public Guid DisputeId { get; set; }
    public Guid ContractId { get; set; }

    public DisputeType Type { get; set; }
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;

    public string Description { get; set; } = default!;

    public DisputeParty RaisedByParty { get; set; }
    public Guid RaisedByUserId { get; set; }

    /// <summary>Short operational note from admin when resolving/rejecting.</summary>
    public string? AdminNote { get; set; }

    public DisputeOutcomeFavor OutcomeFavor { get; set; } = DisputeOutcomeFavor.None;

    public Guid? ReviewedByUserId { get; set; }
    public Guid? ResolvedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Admin SLA clock. Does not auto-resolve.</summary>
    public DateTime? SlaDueAt { get; set; }
    public DateTime? UnderReviewAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? RejectedAt { get; set; }

    public Contract Contract { get; set; } = default!;
    public ApplicationUser RaisedByUser { get; set; } = default!;
    public ApplicationUser? ReviewedByUser { get; set; }
    public ApplicationUser? ResolvedByUser { get; set; }

    public ICollection<DisputeEvidence> Evidence { get; set; } = new List<DisputeEvidence>();
    public ICollection<DisputeEvent> Events { get; set; } = new List<DisputeEvent>();
}
