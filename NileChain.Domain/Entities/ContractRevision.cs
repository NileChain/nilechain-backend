using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>
/// One pre-sign negotiation round: previous draft vs the revised body.
/// </summary>
public class ContractRevision
{
    public Guid ContractRevisionId { get; set; }
    public Guid ContractId { get; set; }
    public string PreviousText { get; set; } = string.Empty;
    public string NewText { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public Guid RevisedByUserId { get; set; }
    public DisputeParty RevisedByParty { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
}
