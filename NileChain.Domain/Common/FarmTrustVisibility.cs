namespace NileChain.Domain.Common;

/// <summary>
/// Who may read a farm trust/risk report. Policy lock from CEO_MARKET_GAPS.
/// </summary>
public static class FarmTrustVisibility
{
    public static bool FactoryMayView(
        bool hasActiveMatchOrRequest,
        bool farmHasPublishedListing) =>
        hasActiveMatchOrRequest || farmHasPublishedListing;
}
