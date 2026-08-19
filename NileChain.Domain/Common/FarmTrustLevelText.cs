namespace NileChain.Domain.Common;

/// <summary>
/// Arabic risk-level labels for a trust band. The score is trust (higher is better),
/// so the label is deliberately inverted: high trust reads as low risk.
/// </summary>
public static class FarmTrustLevelText
{
    public const string LowRisk = "منخفض المخاطر";
    public const string MediumRisk = "متوسط المخاطر";
    public const string HighRisk = "عالي المخاطر";

    public static string Arabic(FarmTrustBand band) => band switch
    {
        FarmTrustBand.High => LowRisk,
        FarmTrustBand.Medium => MediumRisk,
        _ => HighRisk
    };

    /// <summary>Unscored farms are presented as medium rather than high risk.</summary>
    public static string Arabic(decimal? overallScore) =>
        overallScore is decimal score ? Arabic(FarmTrustScore.ToBand(score)) : MediumRisk;
}
