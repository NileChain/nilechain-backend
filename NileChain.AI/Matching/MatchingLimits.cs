namespace NileChain.AI.Matching;

/// <summary>
/// Matching knobs via <c>Matching:*</c> configuration (not orchestration <c>radiusKm</c>).
/// </summary>
public static class MatchingLimits
{
    public const int DefaultMaxResults = 5;
    public const int MaxShowMoreResults = 15;

    /// <summary>
    /// Default haversine radius for <see cref="GeographicMatching.Scope.Nearby"/>.
    /// Distinct from <c>OrchestrationRunState.DefaultRadiusKm</c> (Nationwide widen choreography only).
    /// </summary>
    public const double DefaultNearbyRadiusKm = 50;

    public static int ResolveMaxResults(int? configured) =>
        configured is > 0 ? Math.Min(configured.Value, MaxShowMoreResults) : DefaultMaxResults;

    public static double ResolveNearbyRadiusKm(double? configured) =>
        configured is > 0 ? configured.Value : DefaultNearbyRadiusKm;
}
