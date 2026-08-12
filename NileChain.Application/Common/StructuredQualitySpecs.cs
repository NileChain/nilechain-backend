using System.Globalization;
using NileChain.Application.Dtos.Factory;

namespace NileChain.Application.Common;

/// <summary>
/// Packs/parses structured quality fields into <c>SupplyRequest.QualitySpecs</c>
/// while preserving <c>Gov:</c> / <c>GeoScope:</c> markers used by matching.
/// </summary>
public static class StructuredQualitySpecs
{
    private const string MoistureKey = "MoistureMax:";
    private const string ImpuritiesKey = "ImpuritiesMax:";
    private const string GradeKey = "Grade:";
    private const string LabKey = "LabRequired:";
    private const string PreferredFarmKey = "PreferredFarm:";
    private const string GovKey = "Gov:";
    private const string GeoScopeKey = "GeoScope:";

    public static string Pack(
        string? freeTextNotes,
        StructuredQualityInput? structured,
        IReadOnlyList<string> governorates,
        string geographicScope,
        Guid? preferredFarmId = null)
    {
        var parts = new List<string>();

        if (structured?.MoistureMaxPercent is { } moisture)
            parts.Add($"{MoistureKey}{moisture.ToString(CultureInfo.InvariantCulture)}");

        if (structured?.ImpuritiesMaxPercent is { } impurities)
            parts.Add($"{ImpuritiesKey}{impurities.ToString(CultureInfo.InvariantCulture)}");

        if (!string.IsNullOrWhiteSpace(structured?.Grade))
            parts.Add($"{GradeKey}{structured.Grade.Trim()}");

        if (structured?.LabRequired is { } lab)
            parts.Add($"{LabKey}{(lab ? "true" : "false")}");

        if (preferredFarmId is { } farmId && farmId != Guid.Empty)
            parts.Add($"{PreferredFarmKey}{farmId:D}");

        var notes = (structured?.Notes ?? freeTextNotes)?.Trim();
        if (!string.IsNullOrWhiteSpace(notes))
        {
            var cleaned = StripKnownMarkers(notes);
            if (!string.IsNullOrWhiteSpace(cleaned))
                parts.Add(cleaned);
        }

        if (governorates.Count > 0)
            parts.Add($"{GovKey}{string.Join(",", governorates)}");

        parts.Add($"{GeoScopeKey}{geographicScope}");

        return string.Join(" | ", parts);
    }

    public static StructuredQualitySpecsDto Parse(string? qualitySpecs)
    {
        var dto = new StructuredQualitySpecsDto
        {
            Raw = qualitySpecs,
            PreferredGovernorates = ParseGovernorates(qualitySpecs),
            GeographicScope = ParseScope(qualitySpecs)
        };

        if (string.IsNullOrWhiteSpace(qualitySpecs))
            return dto;

        dto.MoistureMaxPercent = ReadDecimal(qualitySpecs, MoistureKey);
        dto.ImpuritiesMaxPercent = ReadDecimal(qualitySpecs, ImpuritiesKey);
        dto.Grade = ReadToken(qualitySpecs, GradeKey);
        dto.LabRequired = ReadBool(qualitySpecs, LabKey);
        dto.PreferredFarmId = ReadGuid(qualitySpecs, PreferredFarmKey);
        dto.Notes = StripKnownMarkers(qualitySpecs);

        return dto;
    }

    private static List<string> ParseGovernorates(string? qualitySpecs)
    {
        var result = new List<string>();
        var segment = ReadToken(qualitySpecs, GovKey);
        if (segment is null)
            return result;

        foreach (var part in segment.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!result.Contains(part, StringComparer.OrdinalIgnoreCase))
                result.Add(part);
        }

        return result;
    }

    private static string ParseScope(string? qualitySpecs)
    {
        var token = ReadToken(qualitySpecs, GeoScopeKey);
        if (string.Equals(token, "Nearby", StringComparison.OrdinalIgnoreCase))
            return "Nearby";
        if (string.Equals(token, "Nationwide", StringComparison.OrdinalIgnoreCase))
            return "Nationwide";
        if (string.Equals(token, "Exact", StringComparison.OrdinalIgnoreCase))
            return "Exact";
        return ParseGovernorates(qualitySpecs).Count > 0 ? "Exact" : "Nationwide";
    }

    private static string? StripKnownMarkers(string qualitySpecs)
    {
        var segments = qualitySpecs
            .Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Where(s =>
                !s.StartsWith(GovKey, StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith(GeoScopeKey, StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith(MoistureKey, StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith(ImpuritiesKey, StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith(GradeKey, StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith(LabKey, StringComparison.OrdinalIgnoreCase)
                && !s.StartsWith(PreferredFarmKey, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return segments.Count == 0 ? null : string.Join(" | ", segments);
    }

    private static string? ReadToken(string? specs, string marker)
    {
        if (string.IsNullOrWhiteSpace(specs))
            return null;
        var idx = specs.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return null;
        var start = idx + marker.Length;
        var end = specs.IndexOf('|', start);
        var token = (end >= 0 ? specs[start..end] : specs[start..]).Trim();
        return string.IsNullOrWhiteSpace(token) ? null : token;
    }

    private static decimal? ReadDecimal(string specs, string marker)
    {
        var token = ReadToken(specs, marker);
        if (token is null)
            return null;
        return decimal.TryParse(token, NumberStyles.Number, CultureInfo.InvariantCulture, out var v)
            ? v
            : null;
    }

    private static bool? ReadBool(string specs, string marker)
    {
        var token = ReadToken(specs, marker);
        if (token is null)
            return null;
        if (bool.TryParse(token, out var b))
            return b;
        if (token is "1" or "yes" or "y")
            return true;
        if (token is "0" or "no" or "n")
            return false;
        return null;
    }

    private static Guid? ReadGuid(string specs, string marker)
    {
        var token = ReadToken(specs, marker);
        return Guid.TryParse(token, out var id) ? id : null;
    }
}
