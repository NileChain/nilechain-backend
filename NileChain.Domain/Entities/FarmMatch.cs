using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

public class FarmMatch
{
    public Guid MatchId { get; set; }
    public Guid RequestId { get; set; }
    public Guid FarmId { get; set; }
    public decimal? MatchScore { get; set; }
    public decimal? RiskScore { get; set; }
    /// <summary>Farm governorate captured when the match was Proposed (Exact-scope revalidation).</summary>
    public string? MatchedGovernorate { get; set; }
    public FarmMatchStatus Status { get; set; } = FarmMatchStatus.Proposed;

    /// <summary>
    /// Factory explicitly excluded this farm for this supply request.
    /// Survives agent re-runs (matching skips the farm; upsert will not revive).
    /// </summary>
    public bool IsExcludedByFactory { get; set; }

    /// <summary>
    /// Immutable JSON eligibility snapshot captured when the match was Proposed
    /// (governorate, crops, commercial terms, risk, verified).
    /// </summary>
    public string? EligibilitySnapshotJson { get; set; }

    /// <summary>Farm counter-offer quantity (tons). Null = no counter on quantity.</summary>
    public decimal? CounterQuantityTons { get; set; }
    public decimal? CounterPricePerTon { get; set; }
    public DateTime? CounterDeliveryDate { get; set; }
    public string? CounterNote { get; set; }
    public DateTime? CounteredAt { get; set; }
    /// <summary>Factory accepted the farm counter terms (contract uses effective counter values).</summary>
    public bool CounterAccepted { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SupplyRequest SupplyRequest { get; set; } = default!;
    public Farm Farm { get; set; } = default!;
    public Contract? Contract { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
