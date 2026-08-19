using NileChain.Domain.Common;

namespace NileChain.AI.Evaluation;

/// <summary>
/// A farm described by the trust band its profile should compute to, not by a hand-picked
/// number — so the golden set exercises the real <see cref="FarmTrustScore"/> formula
/// instead of a value the seeder invented.
/// </summary>
public sealed record GoldenFarm(
    string Name,
    string Governorate,
    FarmTrustBand Trust,
    bool Verified,
    decimal AvailableTons = 100m);

/// <summary>The world a scenario is run against: one factory, one crop, and a set of farms.</summary>
public sealed record GoldenWorld(
    string Crop,
    string FactoryGovernorate,
    string GeoScope,
    IReadOnlyList<GoldenFarm> Farms,
    bool FactoryApprovedOneRingExpansion = false,
    int? ShortlistTakeLimit = null,
    decimal QuantityTons = 10m)
{
    /// <summary>The request's QualitySpecs doubles as the geo-scope carrier in this codebase.</summary>
    public string QualitySpecs => $"Gov:{FactoryGovernorate} | GeoScope:{GeoScope}";
}

/// <summary>
/// What a correct run must look like. Everything here is deterministic, so a diff means a
/// real behaviour change rather than model drift.
/// </summary>
public sealed record GoldenExpectation(
    int TopMatchCount,
    int TotalEligible,
    int TruncatedCount,
    IReadOnlyList<string> AllowedGovernorates,
    IReadOnlyList<string> RequiredTrailFunctions,
    IReadOnlyList<string> ForbiddenTrailFunctions,
    bool Success = true,
    string? PeekGovernorate = null,
    bool ExpectRiskWarning = false);

public sealed record GoldenScenario(
    string Name,
    string Intent,
    GoldenWorld World,
    GoldenExpectation Expect);
