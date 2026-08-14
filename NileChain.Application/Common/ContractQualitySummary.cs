using System.Globalization;
using System.Text;

namespace NileChain.Application.Common;

/// <summary>
/// Human-readable quality summary from packed <c>QualitySpecs</c>.
/// Only includes fields that actually exist — never invents specs.
/// </summary>
public static class ContractQualitySummary
{
    public static string? Format(string? qualitySpecs)
    {
        if (string.IsNullOrWhiteSpace(qualitySpecs))
            return null;

        var parsed = StructuredQualitySpecs.Parse(qualitySpecs);
        var parts = new List<string>();

        if (parsed.MoistureMaxPercent is { } moisture)
            parts.Add($"Moisture ≤ {moisture.ToString(CultureInfo.InvariantCulture)}%");

        if (parsed.ImpuritiesMaxPercent is { } impurities)
            parts.Add($"Impurities ≤ {impurities.ToString(CultureInfo.InvariantCulture)}%");

        if (!string.IsNullOrWhiteSpace(parsed.Grade))
            parts.Add($"Grade: {parsed.Grade.Trim()}");

        if (parsed.LabRequired is true)
            parts.Add("Lab inspection required");
        else if (parsed.LabRequired is false)
            parts.Add("Lab inspection not required");

        if (!string.IsNullOrWhiteSpace(parsed.Notes))
            parts.Add(parsed.Notes.Trim());

        if (parts.Count == 0)
        {
            // Fallback: show raw only when it is short free text without packing markers.
            var raw = qualitySpecs.Trim();
            if (raw.Length <= 240 && !raw.Contains("GeoScope:", StringComparison.OrdinalIgnoreCase))
                return raw;
            return null;
        }

        return string.Join(" · ", parts);
    }
}
