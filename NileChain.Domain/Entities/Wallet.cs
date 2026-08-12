using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>Platform wallet for a factory or farm (EGP ledger balance).</summary>
public class Wallet
{
    public Guid WalletId { get; set; }
    public WalletOwnerType OwnerType { get; set; }
    public Guid OwnerId { get; set; }

    /// <summary>Spendable / withdrawable balance.</summary>
    public decimal AvailableBalanceEgp { get; set; }

    /// <summary>Funds locked in escrow holds (factory side).</summary>
    public decimal HeldBalanceEgp { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public ICollection<WalletLedgerEntry> Ledger { get; set; } = new List<WalletLedgerEntry>();
}

public class WalletLedgerEntry
{
    public Guid LedgerEntryId { get; set; }
    public Guid WalletId { get; set; }
    public WalletLedgerType EntryType { get; set; }
    public decimal AmountEgp { get; set; }
    public decimal AvailableAfterEgp { get; set; }
    public decimal HeldAfterEgp { get; set; }
    public string Currency { get; set; } = "EGP";
    public string? Description { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Wallet Wallet { get; set; } = default!;
}

/// <summary>Factory/farm top-up via Paymob Intention (card in sandbox).</summary>
public class WalletTopUp
{
    public Guid TopUpId { get; set; }
    public Guid WalletId { get; set; }
    public Guid UserId { get; set; }
    public decimal AmountEgp { get; set; }
    public WalletTopUpStatus Status { get; set; } = WalletTopUpStatus.Created;
    public string? IdempotencyKey { get; set; }

    public string? PaymobIntentionId { get; set; }
    public string? PaymobOrderId { get; set; }
    public string? PaymobTransactionId { get; set; }
    public string? ClientSecret { get; set; }
    public string? CheckoutUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }

    public Wallet Wallet { get; set; } = default!;
}

/// <summary>Farm (or factory) cash-out from platform wallet.</summary>
public class WalletWithdrawal
{
    public Guid WithdrawalId { get; set; }
    public Guid WalletId { get; set; }
    public Guid UserId { get; set; }
    public decimal AmountEgp { get; set; }
    public WalletWithdrawalStatus Status { get; set; } = WalletWithdrawalStatus.Pending;
    public string Method { get; set; } = "BankTransfer";
    public string? DestinationSummary { get; set; }
    public string? FailReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public Wallet Wallet { get; set; } = default!;
}
