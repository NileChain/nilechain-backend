namespace NileChain.Domain.Enums;

/// <summary>
/// Mock escrow lifecycle (demo gateway). Not a real payment processor settlement.
/// </summary>
public enum EscrowStatus
{
    Created,
    Pending,
    Held,
    Released,
    Refunded,
    Failed
}
