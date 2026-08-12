namespace NileChain.Application.Contracts;

/// <summary>
/// Platform fee on a commercial amount. Fee is charged on top of the farm net
/// (factory pays amount + fee; farm still receives the full amount).
/// </summary>
public static class PlatformFeeMath
{
    public const decimal MaxPercent = 30m;

    public static decimal ClampPercent(decimal percent) =>
        percent < 0 ? 0 : Math.Min(percent, MaxPercent);

    public static decimal FeeOn(decimal amountEgp, decimal percent)
    {
        amountEgp = decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero);
        return decimal.Round(
            amountEgp * ClampPercent(percent) / 100m,
            2,
            MidpointRounding.AwayFromZero);
    }

    /// <summary>Factory debit for a farm-net amount (milestone or full deal).</summary>
    public static decimal TotalCharged(decimal amountEgp, decimal percent) =>
        decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero) + FeeOn(amountEgp, percent);
}
