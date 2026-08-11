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
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SupplyRequest SupplyRequest { get; set; } = default!;
    public Farm Farm { get; set; } = default!;
    public Contract? Contract { get; set; }
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
