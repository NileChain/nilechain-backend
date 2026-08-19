namespace NileChain.AI.Models;

public class MatchResult
{
    public Guid FarmId { get; set; }
    /// <summary>Persisted FarmMatch id when available after orchestrator save.</summary>
    public Guid? MatchId { get; set; }
    public string FarmName { get; set; } = string.Empty;
    public string Governorate { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public decimal RiskScore { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public List<string> CropTypes { get; set; } = new();

    /// <summary>
    /// Factory↔farm great-circle distance when both have coordinates; null when unavailable.
    /// </summary>
    public double? DistanceKm { get; set; }

    /// <summary>
    /// True when Nearby matching fell back to governorate eligibility because lat/long was missing.
    /// </summary>
    public bool UsedGovernorateFallback { get; set; }

    /// <summary>
    /// True when the farm is outside the factory's original GeoScope and only included
    /// after an explicit one-ring expansion opt-in.
    /// </summary>
    public bool IsGeographicExpansion { get; set; }

    /// <summary>
    /// True when the farm's governorate is one the factory preferred, which is what earns
    /// the location points. Persisted into the eligibility snapshot to explain the score later.
    /// </summary>
    public bool LocationMatched { get; set; }

    /// <summary>
    /// Full risk breakdown for this farm (additive; existing score fields unchanged).
    /// </summary>
    public RiskReport? RiskReport { get; set; }
}
