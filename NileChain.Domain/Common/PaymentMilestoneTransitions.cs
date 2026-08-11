using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

/// <summary>
/// Payment milestone status-tracking transitions (not fund movement).
/// Factory: Pending → MarkedPaid. Farm: MarkedPaid → Completed.
/// </summary>
public static class PaymentMilestoneTransitions
{
    public static bool IsTerminal(TransactionStatus status) =>
        status is TransactionStatus.Voided;

    /// <summary>
    /// Regen/cancel voids every non-voided milestone — including Completed —
    /// because Amount is derived from qty×price and would go stale (unlike fulfillment).
    /// </summary>
    public static bool CanVoid(TransactionStatus from) =>
        from != TransactionStatus.Voided;

    public static bool CanTransition(TransactionStatus from, TransactionStatus to) =>
        (from, to) switch
        {
            (TransactionStatus.Pending, TransactionStatus.MarkedPaid) => true,
            (TransactionStatus.MarkedPaid, TransactionStatus.Completed) => true,
            (_, TransactionStatus.Voided) when CanVoid(from) => true,
            _ => false
        };

    public static bool IsFactoryAction(TransactionStatus to) =>
        to == TransactionStatus.MarkedPaid;

    public static bool IsFarmAction(TransactionStatus to) =>
        to == TransactionStatus.Completed;
}
