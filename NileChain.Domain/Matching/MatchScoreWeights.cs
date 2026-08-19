namespace NileChain.Domain.Matching;

/// <summary>
/// Single source of truth for the deterministic 0–100 match score.
/// Crop match is a hard filter, so every shortlisted farm already owns those points.
/// Kept in the domain so the factory-facing explanation can never drift from the
/// score that actually ranked the shortlist.
/// </summary>
public static class MatchScoreWeights
{
    public const decimal CropMatch = 40m;
    public const decimal LocationMatch = 20m;
    public const decimal VerifiedFarm = 20m;
    public const decimal TrustMax = 20m;
    public const decimal Max = 100m;

    /// <summary>Proportional: a trust score of 75/100 earns 15 of the 20 trust points.</summary>
    public static decimal TrustPoints(decimal trustScore) =>
        (Math.Clamp(trustScore, 0m, 100m) / 100m) * TrustMax;

    public static decimal Compute(bool locationMatched, bool isVerified, decimal trustScore)
    {
        var score = CropMatch;

        if (locationMatched)
            score += LocationMatch;

        if (isVerified)
            score += VerifiedFarm;

        return score + TrustPoints(trustScore);
    }
}
