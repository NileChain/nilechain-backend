namespace NileChain.AI.Matching;

/// <summary>
/// Deterministic geographic policy for farm matching.
/// AI/LLM must not override these hard constraints.
/// </summary>
public static class GeographicMatching
{
    public enum Scope
    {
        /// <summary>Only farms in the selected/preferred governorate(s).</summary>
        Exact = 0,
        /// <summary>
        /// Primary filter: haversine within <c>Matching:NearbyRadiusKm</c>.
        /// Missing coords fall back to preferred + adjacent governorates via <see cref="IsEligible"/>.
        /// </summary>
        Nearby = 1,
        /// <summary>No geographic hard filter (nationwide).</summary>
        Nationwide = 2
    }

    private static readonly Dictionary<string, string> Aliases =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["alex"] = "Alexandria",
            ["alexandria"] = "Alexandria",
            ["assiut"] = "Asyut",
            ["asyut"] = "Asyut",
            ["fayoum"] = "Faiyum",
            ["faiyum"] = "Faiyum",
            ["cairo"] = "Cairo",
            ["giza"] = "Giza",
            ["beheira"] = "Beheira",
            ["minya"] = "Minya",
            ["luxor"] = "Luxor",
            ["sharqia"] = "Sharqia",
            ["dakahlia"] = "Dakahlia",
            ["gharbia"] = "Gharbia",
            ["monufia"] = "Monufia",
            ["qalyubia"] = "Qalyubia",
            ["kafr el sheikh"] = "Kafr El Sheikh",
            ["kafr"] = "Kafr El Sheikh",
            ["port said"] = "Port Said",
            ["port"] = "Port Said",
            ["beni suef"] = "Beni Suef",
            ["new valley"] = "New Valley",
            ["north sinai"] = "North Sinai",
            ["south sinai"] = "South Sinai",
            ["red sea"] = "Red Sea",
            ["suez"] = "Suez",
            ["ismailia"] = "Ismailia",
            ["damietta"] = "Damietta",
            ["matrouh"] = "Matrouh",
            ["aswan"] = "Aswan",
            ["qena"] = "Qena",
            ["sohag"] = "Sohag"
        };

    /// <summary>Approximate adjacency for Nearby mode (canonical English names).</summary>
    private static readonly Dictionary<string, string[]> Adjacency =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Cairo"] = ["Giza", "Qalyubia", "Sharqia"],
            ["Giza"] = ["Cairo", "Qalyubia", "Faiyum", "Beni Suef", "Monufia"],
            ["Alexandria"] = ["Beheira", "Matrouh"],
            ["Beheira"] = ["Alexandria", "Kafr El Sheikh", "Gharbia", "Monufia", "Matrouh"],
            ["Minya"] = ["Beni Suef", "Faiyum", "Asyut"],
            ["Sharqia"] = ["Cairo", "Qalyubia", "Dakahlia", "Ismailia", "Suez"],
            ["Qalyubia"] = ["Cairo", "Giza", "Sharqia", "Monufia", "Dakahlia"],
            ["Monufia"] = ["Giza", "Beheira", "Gharbia", "Qalyubia"],
            ["Gharbia"] = ["Beheira", "Kafr El Sheikh", "Dakahlia", "Monufia"],
            ["Dakahlia"] = ["Sharqia", "Gharbia", "Kafr El Sheikh", "Damietta", "Qalyubia"],
            ["Kafr El Sheikh"] = ["Beheira", "Gharbia", "Dakahlia"],
            ["Damietta"] = ["Dakahlia", "Port Said"],
            ["Port Said"] = ["Damietta", "Ismailia", "Sharqia"],
            ["Ismailia"] = ["Sharqia", "Port Said", "Suez", "North Sinai"],
            ["Suez"] = ["Cairo", "Sharqia", "Ismailia", "Red Sea"],
            ["Faiyum"] = ["Giza", "Beni Suef", "Minya"],
            ["Beni Suef"] = ["Giza", "Faiyum", "Minya"],
            ["Asyut"] = ["Minya", "Sohag", "New Valley", "Red Sea"],
            ["Sohag"] = ["Asyut", "Qena", "Red Sea"],
            ["Qena"] = ["Sohag", "Luxor", "Red Sea"],
            ["Luxor"] = ["Qena", "Aswan", "Red Sea"],
            ["Aswan"] = ["Luxor", "Red Sea", "New Valley"],
            ["Matrouh"] = ["Alexandria", "Beheira", "New Valley"],
            ["New Valley"] = ["Matrouh", "Asyut", "Aswan", "Red Sea"],
            ["Red Sea"] = ["Suez", "Asyut", "Sohag", "Qena", "Luxor", "Aswan", "New Valley"],
            ["North Sinai"] = ["Ismailia", "Port Said", "South Sinai", "Suez"],
            ["South Sinai"] = ["North Sinai", "Suez", "Red Sea"]
        };

    public static string? NormalizeGovernorate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        if (Aliases.TryGetValue(trimmed, out var alias))
            return alias;

        // Title-case-ish: match alias keys that are substrings.
        foreach (var (key, canonical) in Aliases)
        {
            if (trimmed.Contains(key, StringComparison.OrdinalIgnoreCase)
                || key.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return canonical;
            }
        }

        // Preserve unknown values in a stable casing for equality checks.
        return trimmed;
    }

    public static Scope ParseScope(string? qualitySpecs, bool hasPreferredGovernorates)
    {
        if (!string.IsNullOrWhiteSpace(qualitySpecs))
        {
            var marker = "GeoScope:";
            var idx = qualitySpecs.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = idx + marker.Length;
                var end = qualitySpecs.IndexOf('|', start);
                var token = (end >= 0
                        ? qualitySpecs[start..end]
                        : qualitySpecs[start..])
                    .Trim();

                if (Enum.TryParse<Scope>(token, ignoreCase: true, out var parsed))
                    return parsed;
            }
        }

        // Default: Exact when the factory expressed a location preference; otherwise Nationwide.
        return hasPreferredGovernorates ? Scope.Exact : Scope.Nationwide;
    }

    /// <summary>
    /// Preferred governorates from <c>Gov:...</c> in QualitySpecs, falling back to factory profile.
    /// </summary>
    public static IReadOnlyList<string> ParsePreferredGovernorates(
        string? qualitySpecs,
        string? factoryGovernorate)
    {
        var result = new List<string>();

        if (!string.IsNullOrWhiteSpace(qualitySpecs))
        {
            var marker = "Gov:";
            var idx = qualitySpecs.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = idx + marker.Length;
                var end = qualitySpecs.IndexOf('|', start);
                var segment = (end >= 0
                        ? qualitySpecs[start..end]
                        : qualitySpecs[start..])
                    .Trim();

                foreach (var part in segment.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var normalized = NormalizeGovernorate(part);
                    if (!string.IsNullOrWhiteSpace(normalized)
                        && !result.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        result.Add(normalized);
                    }
                }
            }
        }

        if (result.Count == 0)
        {
            var factoryGov = NormalizeGovernorate(factoryGovernorate);
            if (!string.IsNullOrWhiteSpace(factoryGov))
                result.Add(factoryGov);
        }

        return result;
    }

    public static IReadOnlyList<string> GetEligibleGovernorates(
        IReadOnlyList<string> preferred,
        Scope scope)
    {
        if (scope == Scope.Nationwide || preferred.Count == 0)
            return Array.Empty<string>(); // empty = unrestricted

        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in preferred)
        {
            var n = NormalizeGovernorate(g);
            if (!string.IsNullOrWhiteSpace(n))
                set.Add(n);
        }

        if (scope == Scope.Nearby)
        {
            foreach (var g in preferred.ToList())
            {
                var n = NormalizeGovernorate(g);
                if (n is null)
                    continue;
                if (Adjacency.TryGetValue(n, out var neighbors))
                {
                    foreach (var neighbor in neighbors)
                        set.Add(neighbor);
                }
            }
        }

        return set.ToList();
    }

    /// <summary>
    /// Returns true if the farm passes the hard geographic constraint.
    /// Nationwide always passes.
    /// Exact/Nearby with no preferred governorates fail closed (no silent Nationwide).
    /// Farms with null/unknown governorate fail Exact/Nearby when locations were requested.
    /// </summary>
    /// <summary>
    /// Governorate-set eligibility. For <see cref="Scope.Nearby"/> this is the
    /// preferred+adjacency set used as the missing-coordinate fallback (and Exact/Nationwide
    /// hard rules). Primary Nearby filtering uses haversine in MatchingPlugin.
    /// </summary>
    public static bool IsEligible(
        string? farmGovernorate,
        IReadOnlyList<string> preferred,
        Scope scope)
    {
        if (scope == Scope.Nationwide)
            return true;

        // Exact/Nearby without preferred locations must not behave like Nationwide.
        if (preferred.Count == 0)
            return false;

        var eligible = GetEligibleGovernorates(preferred, scope);
        if (eligible.Count == 0)
            return false;

        var farm = NormalizeGovernorate(farmGovernorate);
        if (string.IsNullOrWhiteSpace(farm))
            return false;

        return eligible.Any(e => string.Equals(e, farm, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsPreferredMatch(string? farmGovernorate, IReadOnlyList<string> preferred)
    {
        var farm = NormalizeGovernorate(farmGovernorate);
        if (string.IsNullOrWhiteSpace(farm) || preferred.Count == 0)
            return false;

        return preferred.Any(p =>
            string.Equals(NormalizeGovernorate(p), farm, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Persisted factory scope is authoritative. Tool/LLM overrides may only tighten scope,
    /// never broaden (Exact→Nearby/Nationwide or Nearby→Nationwide).
    /// </summary>
    public static Scope ResolveEffectiveScope(Scope persisted, Scope? requestedOverride)
    {
        if (requestedOverride is null)
            return persisted;

        // Enum order: Exact=0 < Nearby=1 < Nationwide=2 — higher = broader.
        if ((int)requestedOverride.Value > (int)persisted)
            return persisted;

        return requestedOverride.Value;
    }

    /// <summary>
    /// Automatic geographic expansion (WidenSearchRadius / radius→Nationwide) is only
    /// meaningful when the factory already chose Nationwide. Exact and Nearby never expand.
    /// </summary>
    public static bool AllowsAutomaticGeographicExpansion(Scope persisted) =>
        persisted == Scope.Nationwide;
}
