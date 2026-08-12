namespace NileChain.Domain.Enums;

/// <summary>
/// Payment milestone tracking statuses. Not settlement states — no real funds move.
/// </summary>
public enum TransactionStatus
{
    Pending,
    /// <summary>Factory marked this milestone as paid (status claim only).</summary>
    MarkedPaid,
    /// <summary>Mock escrow: factory paid via demo gateway; funds conceptually held.</summary>
    EscrowHeld,
    /// <summary>Farm confirmed payment received (status claim only).</summary>
    Completed,
    Failed,
    Refunded,
    /// <summary>Schedule voided after contract regen/cancel.</summary>
    Voided
}
