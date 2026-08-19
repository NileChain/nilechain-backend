namespace NileChain.AI.Models;

/// <summary>
/// Optional "better farm one ring out" hint. Never mixed into <see cref="AgentResponse.TopMatches"/>.
/// </summary>
public class PeekHint
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public decimal RiskScore { get; set; }
    public bool IsVerified { get; set; }
    public double? DistanceKm { get; set; }

    /// <summary>EmptyPrimary | Verified | HigherScore | HigherTrust</summary>
    public string Reason { get; set; } = string.Empty;
}
