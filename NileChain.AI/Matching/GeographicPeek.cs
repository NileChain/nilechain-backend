using NileChain.AI.Models;

namespace NileChain.AI.Matching;

/// <summary>
/// One-ring geographic peek: surface a better farm just outside the factory's
/// committed GeoScope. The LLM never triggers this — the factory opts in.
/// </summary>
public static class GeographicPeek
{
    public const decimal ScoreDelta = 10m;
    public const decimal TrustDelta = 15m;
    public const double NearbyExtraKm = 25;
    /// <summary>Banner only when the extra farm is slightly farther, not a distant-governorate jump.</summary>
    public const double MaxExtraDistanceKm = 80;

    public const string ReasonEmptyPrimary = "EmptyPrimary";
    public const string ReasonVerified = "Verified";
    public const string ReasonHigherScore = "HigherScore";
    public const string ReasonHigherTrust = "HigherTrust";

    public static double ExpandedNearbyRadiusKm(double configuredNearbyRadiusKm) =>
        configuredNearbyRadiusKm + NearbyExtraKm;

    /// <summary>
    /// Peek uses Nearby for Exact (adjacency / haversine). Nearby uses a larger
    /// radius. Nationwide has no geographic peek.
    /// </summary>
    public static bool TryPeekParameters(
        GeographicMatching.Scope persisted,
        double configuredNearbyRadiusKm,
        out GeographicMatching.Scope peekScope,
        out double peekRadiusKm)
    {
        peekScope = persisted;
        peekRadiusKm = configuredNearbyRadiusKm;

        switch (persisted)
        {
            case GeographicMatching.Scope.Exact:
                peekScope = GeographicMatching.Scope.Nearby;
                peekRadiusKm = configuredNearbyRadiusKm;
                return true;
            case GeographicMatching.Scope.Nearby:
                peekScope = GeographicMatching.Scope.Nearby;
                peekRadiusKm = ExpandedNearbyRadiusKm(configuredNearbyRadiusKm);
                return true;
            default:
                return false;
        }
    }

    public static PeekHint? Pick(
        IReadOnlyList<MatchResult> primary,
        IReadOnlyList<MatchResult> shadow)
    {
        var primaryIds = primary.Select(p => p.FarmId).ToHashSet();
        var outside = shadow
            .Where(s => !primaryIds.Contains(s.FarmId))
            .OrderByDescending(s => s.MatchScore)
            .ThenByDescending(s => s.RiskScore)
            .ThenByDescending(s => s.IsVerified)
            .ThenBy(s => s.DistanceKm ?? double.MaxValue)
            .ThenBy(s => s.FarmId)
            .ToList();

        if (outside.Count == 0)
            return null;

        if (primary.Count == 0)
        {
            var closest = outside
                .OrderBy(s => s.DistanceKm ?? double.MaxValue)
                .ThenByDescending(s => s.MatchScore)
                .ThenByDescending(s => s.RiskScore)
                .First();
            return ToHint(closest, ReasonEmptyPrimary);
        }

        var bestPrimary = primary
            .OrderByDescending(p => p.MatchScore)
            .ThenByDescending(p => p.RiskScore)
            .First();

        var minPrimaryDistance = primary
            .Where(p => p.DistanceKm is not null)
            .Select(p => p.DistanceKm!.Value)
            .DefaultIfEmpty()
            .Min();
        var hasPrimaryDistance = primary.Any(p => p.DistanceKm is not null);

        foreach (var candidate in outside)
        {
            if (hasPrimaryDistance
                && candidate.DistanceKm is double shadowKm
                && shadowKm > minPrimaryDistance + MaxExtraDistanceKm)
            {
                continue;
            }

            if (candidate.IsVerified && primary.All(p => !p.IsVerified))
                return ToHint(candidate, ReasonVerified);

            if (candidate.MatchScore >= bestPrimary.MatchScore + ScoreDelta)
                return ToHint(candidate, ReasonHigherScore);

            if (candidate.RiskScore >= bestPrimary.RiskScore + TrustDelta)
                return ToHint(candidate, ReasonHigherTrust);
        }

        return null;
    }

    private static PeekHint ToHint(MatchResult farm, string reason) =>
        new()
        {
            FarmId = farm.FarmId,
            FarmName = farm.FarmName,
            Governorate = farm.Governorate,
            MatchScore = farm.MatchScore,
            RiskScore = farm.RiskScore,
            IsVerified = farm.IsVerified,
            DistanceKm = farm.DistanceKm,
            Reason = reason
        };
}
