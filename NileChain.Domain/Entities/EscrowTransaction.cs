using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>
/// Mock escrow row for a payment milestone. Holds simulated funds + platform fee snapshot.
/// No real money moves — demo/CEO monetization visibility only.
/// </summary>
public class EscrowTransaction
{
    public Guid EscrowTransactionId { get; set; }
    public Guid ContractId { get; set; }
    public Guid TransactionId { get; set; }

    public Guid FactoryId { get; set; }
    public Guid FarmId { get; set; }

    /// <summary>Milestone commercial amount owed to the farm (snapshot at pay).</summary>
    public decimal MilestoneAmountEgp { get; set; }

    /// <summary>Platform take-rate fee charged to the factory on top of the milestone.</summary>
    public decimal PlatformFeeEgp { get; set; }

    /// <summary>Fee percent used at session time (e.g. 2.5).</summary>
    public decimal PlatformFeePercent { get; set; }

    /// <summary>What the factory "pays" in the mock checkout (= milestone + fee).</summary>
    public decimal TotalChargedEgp { get; set; }

    /// <summary>What the farm receives on release (= milestone amount).</summary>
    public decimal FarmNetEgp { get; set; }

    public string Currency { get; set; } = "EGP";
    public EscrowStatus Status { get; set; } = EscrowStatus.Created;

    public string? IdempotencyKey { get; set; }
    public string Gateway { get; set; } = "Wallet";

    /// <summary>Paymob refs when top-up/gateway path used (optional).</summary>
    public string? PaymobOrderId { get; set; }
    public string? PaymobTransactionId { get; set; }

    /// <summary>Factory wallet ledger entry that funded this hold.</summary>
    public Guid? FundingLedgerEntryId { get; set; }

    public DateTime? HeldAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public DateTime? FailedAt { get; set; }

    public string? ReleaseReason { get; set; }
    public string? RefundReason { get; set; }
    public string? FailReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
    public Transaction Transaction { get; set; } = default!;
}
