using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

/// <summary>
/// Allowed fulfillment edges and role gates.
/// Quality check is optional: Received → Fulfilled is allowed without QualityChecked.
/// </summary>
public static class FulfillmentTransitions
{
    public static bool IsTerminal(FulfillmentStatus status) =>
        status is FulfillmentStatus.Fulfilled
            or FulfillmentStatus.Voided
            or FulfillmentStatus.RejectedAtGate;

    public static bool CanTransition(FulfillmentStatus from, FulfillmentStatus to) =>
        (from, to) switch
        {
            (FulfillmentStatus.Planned, FulfillmentStatus.Shipped) => true,
            (FulfillmentStatus.Shipped, FulfillmentStatus.Received) => true,
            (FulfillmentStatus.Shipped, FulfillmentStatus.RejectedAtGate) => true,
            (FulfillmentStatus.Received, FulfillmentStatus.QualityChecked) => true,
            (FulfillmentStatus.Received, FulfillmentStatus.Fulfilled) => true,
            (FulfillmentStatus.QualityChecked, FulfillmentStatus.Fulfilled) => true,
            // Void from any non-terminal
            (_, FulfillmentStatus.Voided) when !IsTerminal(from) => true,
            _ => false
        };

    /// <summary>Farm may only ship.</summary>
    public static bool IsFarmAction(FulfillmentStatus to) =>
        to == FulfillmentStatus.Shipped;

    /// <summary>Factory may receive, quality-check, or fulfill.</summary>
    public static bool IsFactoryAction(FulfillmentStatus to) =>
        to is FulfillmentStatus.Received
            or FulfillmentStatus.QualityChecked
            or FulfillmentStatus.Fulfilled
            or FulfillmentStatus.RejectedAtGate;
}
