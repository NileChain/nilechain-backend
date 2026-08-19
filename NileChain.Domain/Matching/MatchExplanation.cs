using NileChain.Domain.Common;

namespace NileChain.Domain.Matching;

public enum MatchFactorState
{
    /// <summary>Contributed points to the match score.</summary>
    Earned,
    /// <summary>Was available but not contributed.</summary>
    Missed,
    /// <summary>Context that did not affect the score.</summary>
    Info,
    /// <summary>Something the factory should look at before signing.</summary>
    Caution
}

/// <summary>
/// One line of the factory-facing "why this farm" answer.
/// <see cref="Code"/> is a stable i18n key; the UI owns the wording.
/// </summary>
public sealed record MatchFactor(
    string Code,
    MatchFactorState State,
    decimal? Points = null,
    decimal? MaxPoints = null,
    string? Detail = null);

public sealed record MatchExplanationInputs
{
    public MatchEligibilitySnapshot? Snapshot { get; init; }
    public string? CropName { get; init; }
    public string? FarmGovernorate { get; init; }
    public bool IsVerified { get; init; }
    public decimal? TrustScore { get; init; }
    public decimal? MatchScore { get; init; }
    public double? DistanceKm { get; init; }
    public bool UsedGovernorateFallback { get; init; }
    public bool IsGeographicExpansion { get; init; }
    public decimal? RequestQuantityTons { get; init; }
    public decimal? RequestPricePerTon { get; init; }
}

/// <summary>
/// Rebuilds, without any model call, the reasons a farm reached the shortlist.
/// Prefers the propose-time snapshot over live profile values so the explanation
/// matches the score the factory is actually looking at.
/// </summary>
public static class MatchExplanation
{
    public const string CropCode = "crop";
    public const string LocationCode = "location";
    public const string LocationOutsideCode = "locationOutside";
    public const string VerifiedCode = "verified";
    public const string NotVerifiedCode = "notVerified";
    public const string TrustCode = "trust";
    public const string DistanceCode = "distance";
    public const string GovernorateFallbackCode = "governorateFallback";
    public const string ExpansionCode = "expansion";
    public const string CapacityCode = "capacity";
    public const string CapacityShortCode = "capacityShort";
    public const string PriceFloorCode = "priceFloor";
    public const string PriceFloorAboveCode = "priceFloorAbove";

    public static IReadOnlyList<MatchFactor> Build(MatchExplanationInputs inputs)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var snapshot = inputs.Snapshot;
        var factors = new List<MatchFactor>();

        // Crop is a hard filter, so every shortlisted farm earned these points.
        factors.Add(new MatchFactor(
            CropCode,
            MatchFactorState.Earned,
            MatchScoreWeights.CropMatch,
            MatchScoreWeights.CropMatch,
            inputs.CropName));

        var governorate = snapshot?.Governorate ?? inputs.FarmGovernorate;
        var locationMatched = snapshot?.LocationMatched ?? !inputs.IsGeographicExpansion;
        factors.Add(locationMatched
            ? new MatchFactor(
                LocationCode,
                MatchFactorState.Earned,
                MatchScoreWeights.LocationMatch,
                MatchScoreWeights.LocationMatch,
                governorate)
            : new MatchFactor(
                LocationOutsideCode,
                MatchFactorState.Missed,
                0m,
                MatchScoreWeights.LocationMatch,
                governorate));

        var isVerified = snapshot?.IsVerified ?? inputs.IsVerified;
        factors.Add(isVerified
            ? new MatchFactor(
                VerifiedCode,
                MatchFactorState.Earned,
                MatchScoreWeights.VerifiedFarm,
                MatchScoreWeights.VerifiedFarm)
            : new MatchFactor(
                NotVerifiedCode,
                MatchFactorState.Missed,
                0m,
                MatchScoreWeights.VerifiedFarm));

        var trust = snapshot?.RiskScore ?? inputs.TrustScore;
        if (trust is decimal trustScore)
        {
            factors.Add(new MatchFactor(
                TrustCode,
                trustScore >= FarmTrustScore.MediumTrustThreshold
                    ? MatchFactorState.Earned
                    : MatchFactorState.Caution,
                decimal.Round(MatchScoreWeights.TrustPoints(trustScore), 1),
                MatchScoreWeights.TrustMax,
                $"{decimal.Round(trustScore, 0)}/100"));
        }

        if (inputs.DistanceKm is double km)
        {
            factors.Add(new MatchFactor(
                DistanceCode,
                MatchFactorState.Info,
                Detail: $"{Math.Round(km)}"));
        }
        else if (inputs.UsedGovernorateFallback)
        {
            factors.Add(new MatchFactor(GovernorateFallbackCode, MatchFactorState.Info));
        }

        if (inputs.IsGeographicExpansion)
            factors.Add(new MatchFactor(ExpansionCode, MatchFactorState.Info, Detail: governorate));

        AddCapacityFactor(factors, snapshot?.AvailableQuantityTons, inputs.RequestQuantityTons);
        AddPriceFloorFactor(factors, snapshot?.MinPricePerTon, inputs.RequestPricePerTon);

        return factors;
    }

    private static void AddCapacityFactor(
        List<MatchFactor> factors,
        decimal? availableTons,
        decimal? requestedTons)
    {
        if (availableTons is not decimal available)
            return;

        var detail = requestedTons is decimal requested
            ? $"{decimal.Round(available, 1)}/{decimal.Round(requested, 1)}"
            : $"{decimal.Round(available, 1)}";

        var covers = requestedTons is not decimal need || available >= need;
        factors.Add(new MatchFactor(
            covers ? CapacityCode : CapacityShortCode,
            covers ? MatchFactorState.Info : MatchFactorState.Caution,
            Detail: detail));
    }

    private static void AddPriceFloorFactor(
        List<MatchFactor> factors,
        decimal? minPricePerTon,
        decimal? offeredPricePerTon)
    {
        if (minPricePerTon is not decimal floor)
            return;

        var detail = offeredPricePerTon is decimal offered
            ? $"{decimal.Round(floor, 0)}/{decimal.Round(offered, 0)}"
            : $"{decimal.Round(floor, 0)}";

        var withinBudget = offeredPricePerTon is not decimal price || floor <= price;
        factors.Add(new MatchFactor(
            withinBudget ? PriceFloorCode : PriceFloorAboveCode,
            withinBudget ? MatchFactorState.Info : MatchFactorState.Caution,
            Detail: detail));
    }
}
