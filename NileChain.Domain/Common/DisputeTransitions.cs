using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

/// <summary>
/// Admin-only status edges for operational dispute handling.
/// </summary>
public static class DisputeTransitions
{
    public static bool IsTerminal(DisputeStatus status) =>
        status is DisputeStatus.Resolved or DisputeStatus.Rejected;

    public static bool IsActive(DisputeStatus status) =>
        status is DisputeStatus.Open or DisputeStatus.UnderReview;

    public static bool CanTransition(DisputeStatus from, DisputeStatus to) =>
        (from, to) switch
        {
            (DisputeStatus.Open, DisputeStatus.UnderReview) => true,
            (DisputeStatus.UnderReview, DisputeStatus.Resolved) => true,
            (DisputeStatus.UnderReview, DisputeStatus.Rejected) => true,
            _ => false
        };

    public static bool RequiresOutcomeFavor(DisputeStatus to) =>
        to == DisputeStatus.Resolved;
}
