using System.Text.Json;
using System.Text.Json.Serialization;

namespace NileChain.Domain.Matching;

/// <summary>
/// Immutable commercial/trust snapshot stored on <c>FarmMatch.EligibilitySnapshotJson</c>
/// when a match is Proposed. Used to detect profile gaming before signature.
/// </summary>
public sealed class MatchEligibilitySnapshot
{
    public string? Governorate { get; set; }
    public List<Guid> CropTypeIds { get; set; } = new();
    public decimal? AvailableQuantityTons { get; set; }
    public decimal? MinPricePerTon { get; set; }
    public decimal? RiskScore { get; set; }
    public bool IsVerified { get; set; }
    public string? DeliveryPoint { get; set; }
    public string? FreightPayer { get; set; }
    public string? TransitRisk { get; set; }

    /// <summary>
    /// Whether the farm sat inside the factory's preferred governorates at propose time.
    /// Null on rows written before match explanations existed.
    /// </summary>
    public bool? LocationMatched { get; set; }

    /// <summary>Match score at propose time, so the explanation reflects the ranking that actually ran.</summary>
    public decimal? MatchScore { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static MatchEligibilitySnapshot? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<MatchEligibilitySnapshot>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Material drift: governorate change, verified lost, risk drop &gt; 15,
    /// required crop removed, or commercial floor/qty worsened vs request needs.
    /// </summary>
    public static bool HasMaterialDrift(
        MatchEligibilitySnapshot snapshot,
        MatchEligibilitySnapshot live,
        decimal? requestQuantityTons,
        decimal? requestPricePerTon)
    {
        if (!string.Equals(
                Normalize(snapshot.Governorate),
                Normalize(live.Governorate),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (snapshot.IsVerified && !live.IsVerified)
            return true;

        if (snapshot.RiskScore is decimal snapRisk
            && live.RiskScore is decimal liveRisk
            && snapRisk - liveRisk > 15m)
        {
            return true;
        }

        if (snapshot.CropTypeIds.Count > 0
            && snapshot.CropTypeIds.Any(id => !live.CropTypeIds.Contains(id)))
        {
            return true;
        }

        if (requestQuantityTons is decimal needed
            && live.AvailableQuantityTons is decimal avail
            && avail < needed)
        {
            return true;
        }

        if (requestPricePerTon is decimal offered
            && live.MinPricePerTon is decimal floor
            && floor > offered)
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.DeliveryPoint)
            && !string.Equals(
                Normalize(snapshot.DeliveryPoint),
                Normalize(live.DeliveryPoint),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.FreightPayer)
            && !string.Equals(
                Normalize(snapshot.FreightPayer),
                Normalize(live.FreightPayer),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.TransitRisk)
            && !string.Equals(
                Normalize(snapshot.TransitRisk),
                Normalize(live.TransitRisk),
                StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static string Normalize(string? value) =>
        (value ?? string.Empty).Trim();
}
