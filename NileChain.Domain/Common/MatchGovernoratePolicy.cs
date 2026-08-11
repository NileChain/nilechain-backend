namespace NileChain.Domain.Common;

/// <summary>
/// Exact-scope matches snapshot the farm governorate at propose time.
/// If the farm later moves out of that governorate, Exact contracts/approvals must fail closed.
/// </summary>
public static class MatchGovernoratePolicy
{
    public static bool IsExactScope(string? qualitySpecs)
    {
        if (string.IsNullOrWhiteSpace(qualitySpecs))
            return false;

        const string marker = "GeoScope:";
        var idx = qualitySpecs.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            // Factory create path always writes GeoScope; legacy Exact default when Gov: present.
            return qualitySpecs.Contains("Gov:", StringComparison.OrdinalIgnoreCase);
        }

        var start = idx + marker.Length;
        var end = qualitySpecs.IndexOf('|', start);
        var token = (end >= 0 ? qualitySpecs[start..end] : qualitySpecs[start..]).Trim();
        return token.Equals("Exact", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Returns false when Exact scope requires the live farm governorate to still match the snapshot.
    /// Non-Exact scopes, or missing snapshots, always pass.
    /// </summary>
    public static bool IsSnapshotStillValid(
        string? matchedGovernorate,
        string? currentFarmGovernorate,
        string? qualitySpecs)
    {
        if (!IsExactScope(qualitySpecs))
            return true;

        if (string.IsNullOrWhiteSpace(matchedGovernorate))
            return true;

        var snap = matchedGovernorate.Trim();
        var live = (currentFarmGovernorate ?? string.Empty).Trim();
        return string.Equals(snap, live, StringComparison.OrdinalIgnoreCase);
    }

    public static string? SnapshotFromFarm(string? farmGovernorate) =>
        string.IsNullOrWhiteSpace(farmGovernorate) ? null : farmGovernorate.Trim();
}
