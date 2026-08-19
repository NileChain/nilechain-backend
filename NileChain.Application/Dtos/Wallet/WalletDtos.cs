namespace NileChain.Application.Dtos.Wallet;

public class WalletDto
{
    public Guid WalletId { get; set; }
    public string OwnerType { get; set; } = default!;
    public Guid OwnerId { get; set; }
    public decimal AvailableBalanceEgp { get; set; }
    public decimal HeldBalanceEgp { get; set; }
    public string Currency { get; set; } = "EGP";
    public bool PaymobConfigured { get; set; }
    public bool SimulatorAvailable { get; set; }
    public decimal PlatformFeePercent { get; set; }
    public string FeePayer { get; set; } = "Factory";
    public string FeeBase { get; set; } = "ReleasedAfterQc";
    public string Disclaimer { get; set; } = string.Empty;
    /// <summary>True for factory and farm wallets billed on a monthly Pro plan.</summary>
    public bool SubscriptionApplies { get; set; }
    public bool SubscriptionActive { get; set; }
    public decimal SubscriptionMonthlyUsd { get; set; }
    public decimal SubscriptionMonthlyEgp { get; set; }
    public DateTime? SubscriptionPaidThroughUtc { get; set; }
    public List<WalletLedgerItemDto> RecentLedger { get; set; } = new();
    public List<WalletWithdrawalDto> RecentWithdrawals { get; set; } = new();
}

public class WalletLedgerItemDto
{
    public Guid LedgerEntryId { get; set; }
    public string EntryType { get; set; } = default!;
    public decimal AmountEgp { get; set; }
    public decimal AvailableAfterEgp { get; set; }
    public decimal HeldAfterEgp { get; set; }
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WalletTopUpSessionDto
{
    public Guid TopUpId { get; set; }
    public decimal AmountEgp { get; set; }
    public string Status { get; set; } = default!;
    public string Mode { get; set; } = "Paymob"; // Paymob | Simulator
    public string? CheckoutUrl { get; set; }
    public string? ClientSecret { get; set; }
    public string Disclaimer { get; set; } = string.Empty;
    /// <summary>Sandbox test cards hint for demo.</summary>
    public string? SandboxHint { get; set; }
}

public class WalletWithdrawalDto
{
    public Guid WithdrawalId { get; set; }
    public decimal AmountEgp { get; set; }
    public string Status { get; set; } = default!;
    public string Method { get; set; } = default!;
    public string? DestinationSummary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public class StartWalletTopUpRequest
{
    public decimal AmountEgp { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ReturnUrl { get; set; }
}

public class WalletWithdrawRequest
{
    public decimal AmountEgp { get; set; }
    public string Method { get; set; } = "BankTransfer";
    public string? DestinationSummary { get; set; }
}

/// <summary>
/// Paymob browser redirect query fields (after Unified Checkout).
/// Used when server webhook cannot reach localhost.
/// </summary>
public class ConfirmPaymobTopUpRequest
{
    public string? Hmac { get; set; }
    public Dictionary<string, string?> Query { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
