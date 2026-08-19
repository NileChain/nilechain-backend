using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>
/// One structured commercial offer on a match. <see cref="FarmMatch"/> Counter* fields
/// remain the latest round so existing contract term resolution stays intact.
/// </summary>
public class MatchNegotiationRound
{
    public Guid RoundId { get; set; }
    public Guid MatchId { get; set; }
    public DealParty OfferedBy { get; set; }
    public decimal? QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Grade { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public FarmMatch FarmMatch { get; set; } = default!;
}
