using NileChain.Domain.Entities;

namespace NileChain.Domain.Common;

/// <summary>
/// Trust band. Higher trust means lower supplier risk — the persisted column is
/// named <c>Farm.RiskScore</c> for backwards compatibility but is a 0–100 *trust* score.
/// </summary>
public enum FarmTrustBand
{
    /// <summary>Low trust = high risk (&lt; 40).</summary>
    Low,
    /// <summary>Medium trust = medium risk (40–69).</summary>
    Medium,
    /// <summary>High trust = low risk (&gt;= 70).</summary>
    High
}

public readonly record struct FarmTrustInputs(
    bool HasName,
    bool HasLocation,
    bool HasGovernorate,
    bool HasPhone,
    bool HasSize,
    bool HasDocuments,
    bool HasDescription,
    bool HasImages,
    bool HasCrops,
    int ValidCertificationCount,
    int SignedContractCount,
    decimal AverageRating);

public sealed record FarmTrustBreakdown(
    decimal Profile,
    decimal Certifications,
    decimal ContractHistory,
    decimal Rating,
    decimal Overall,
    FarmTrustBand Band);

/// <summary>
/// Single source of truth for the deterministic supplier trust formula
/// (profile 25 + certifications 25 + contract history 30 + ratings 20).
/// Deterministic by design: no model, no LLM. Callers that persist the result
/// must all use this type so a cached score can never drift from a live one.
/// </summary>
public static class FarmTrustScore
{
    public const decimal ProfileMaxPoints = 25m;
    public const decimal CertificationMaxPoints = 25m;
    public const decimal ContractMaxPoints = 30m;
    public const decimal RatingMaxPoints = 20m;
    public const decimal MaxOverallScore = 100m;

    public const decimal HighTrustThreshold = 70m;
    public const decimal MediumTrustThreshold = 40m;

    private const decimal ProfileNamePoints = 4m;
    private const decimal ProfileLocationPoints = 4m;
    private const decimal ProfileGovernoratePoints = 4m;
    private const decimal ProfilePhonePoints = 3m;
    private const decimal ProfileSizePoints = 4m;
    private const decimal ProfileDocumentsPoints = 2m;
    private const decimal ProfileDescriptionPoints = 1m;
    private const decimal ProfileImagesPoints = 1m;
    private const decimal ProfileCropsPoints = 3m;

    private const decimal PointsPerCertification = 12.5m;
    private const decimal PointsPerSignedContract = 10m;
    private const int MaxRatingValue = 5;

    public static FarmTrustBreakdown Compute(FarmTrustInputs inputs)
    {
        var profile = Clamp(ProfilePoints(inputs), 0m, ProfileMaxPoints);

        var certifications = Clamp(
            inputs.ValidCertificationCount * PointsPerCertification,
            0m,
            CertificationMaxPoints);

        var contracts = Clamp(
            inputs.SignedContractCount * PointsPerSignedContract,
            0m,
            ContractMaxPoints);

        var rating = Clamp(
            (Clamp(inputs.AverageRating, 0m, MaxRatingValue) / MaxRatingValue) * RatingMaxPoints,
            0m,
            RatingMaxPoints);

        var overall = Clamp(profile + certifications + contracts + rating, 0m, MaxOverallScore);

        return new FarmTrustBreakdown(profile, certifications, contracts, rating, overall, ToBand(overall));
    }

    /// <summary>
    /// Builds inputs from a farm loaded with User, FarmCrops, FarmDocuments,
    /// FarmImages, and FarmCertifications. Missing navigations score as absent,
    /// so callers must load them or the score will be understated.
    /// </summary>
    public static FarmTrustInputs InputsFrom(
        Farm farm,
        int signedContractCount,
        decimal averageRating,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(farm);

        var validCerts = farm.FarmCertifications?
            .Count(c => FarmCertificationRules.CountsTowardTrust(c, utcNow)) ?? 0;

        return new FarmTrustInputs(
            HasName: !string.IsNullOrWhiteSpace(farm.Name),
            HasLocation: !string.IsNullOrWhiteSpace(farm.Location),
            HasGovernorate: !string.IsNullOrWhiteSpace(farm.Governorate),
            HasPhone: !string.IsNullOrWhiteSpace(farm.User?.PhoneNumber),
            HasSize: farm.SizeInFeddans is > 0,
            HasDocuments: farm.FarmDocuments is { Count: > 0 },
            HasDescription: !string.IsNullOrWhiteSpace(farm.Description),
            HasImages: farm.FarmImages is { Count: > 0 },
            HasCrops: farm.FarmCrops is { Count: > 0 },
            ValidCertificationCount: validCerts,
            SignedContractCount: signedContractCount,
            AverageRating: averageRating);
    }

    public static FarmTrustBand ToBand(decimal overallScore) => overallScore switch
    {
        >= HighTrustThreshold => FarmTrustBand.High,
        >= MediumTrustThreshold => FarmTrustBand.Medium,
        _ => FarmTrustBand.Low
    };

    private static decimal ProfilePoints(FarmTrustInputs inputs)
    {
        decimal score = 0;
        if (inputs.HasName) score += ProfileNamePoints;
        if (inputs.HasLocation) score += ProfileLocationPoints;
        if (inputs.HasGovernorate) score += ProfileGovernoratePoints;
        if (inputs.HasPhone) score += ProfilePhonePoints;
        if (inputs.HasSize) score += ProfileSizePoints;
        if (inputs.HasDocuments) score += ProfileDocumentsPoints;
        if (inputs.HasDescription) score += ProfileDescriptionPoints;
        if (inputs.HasImages) score += ProfileImagesPoints;
        if (inputs.HasCrops) score += ProfileCropsPoints;
        return score;
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(Math.Max(value, min), max);
}
