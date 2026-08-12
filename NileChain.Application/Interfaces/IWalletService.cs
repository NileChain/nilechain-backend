using NileChain.Application.Common;
using NileChain.Application.Dtos.Wallet;
using NileChain.Domain.Enums;

namespace NileChain.Application.Interfaces;

public interface IWalletService
{
    Task<Result<WalletDto>> GetMineAsync(Guid userId, bool asFarm);
    Task<Result<WalletTopUpSessionDto>> StartTopUpAsync(
        Guid userId,
        bool asFarm,
        decimal amountEgp,
        string? idempotencyKey,
        string? returnUrl);
    Task<Result<WalletDto>> CompleteSimulatorTopUpAsync(Guid userId, bool asFarm, Guid topUpId);
    Task<Result<WalletDto>> ApplyPaymobTopUpSuccessAsync(
        string specialReference,
        string? paymobTransactionId,
        string? paymobOrderId,
        bool success);

    /// <summary>
    /// Confirm a Paymob top-up from the browser redirect query (HMAC-verified).
    /// Covers local/demo when the Paymob webhook cannot reach localhost.
    /// </summary>
    Task<Result<WalletDto>> ConfirmPaymobReturnAsync(
        Guid userId,
        bool asFarm,
        IReadOnlyDictionary<string, string?> query,
        string? hmac);

    Task<Result<WalletWithdrawalDto>> RequestWithdrawalAsync(
        Guid userId,
        bool asFarm,
        decimal amountEgp,
        string method,
        string? destinationSummary);

    /// <summary>Factory: move available → held for milestone escrow (amount = total charged).</summary>
    Task<Result<Guid>> HoldForEscrowAsync(
        Guid factoryId,
        decimal amountEgp,
        Guid escrowTransactionId,
        string description);

    /// <summary>True when factory available balance covers amountEgp.</summary>
    Task<Result> EnsureFactoryAvailableAsync(Guid factoryId, decimal amountEgp);

    /// <summary>Factory debit for a deal total, including platform fee.</summary>
    decimal GetDealHoldAmountEgp(decimal dealTotalEgp);

    /// <summary>Factory: hold full deal total at contract full-sign (Available → Held).</summary>
    Task<Result<Guid>> HoldDealFundsAsync(
        Guid factoryId,
        Guid contractId,
        decimal amountEgp,
        string description);

    /// <summary>Release held factory funds: farm credited with farmNet; fee retained by platform.</summary>
    Task<Result> ReleaseEscrowToFarmAsync(
        Guid factoryId,
        Guid farmId,
        decimal totalHeldEgp,
        decimal farmNetEgp,
        Guid escrowTransactionId);

    /// <summary>Return held funds to factory available.</summary>
    Task<Result> RefundEscrowHoldAsync(
        Guid factoryId,
        decimal totalHeldEgp,
        Guid escrowTransactionId,
        string reason);

    /// <summary>Move factory Held → Available for leftover deal hold / QC / unwind.</summary>
    Task<Result> RefundHeldAmountAsync(
        Guid factoryId,
        decimal amountEgp,
        string referenceType,
        Guid referenceId,
        string reason);

    /// <summary>
    /// Dispute split: deduct totalHeld from factory Held, credit farmShare to farm,
    /// return the remainder to factory Available (platform fee is not kept).
    /// </summary>
    Task<Result> SplitEscrowHoldAsync(
        Guid factoryId,
        Guid farmId,
        decimal totalHeldEgp,
        decimal farmShareEgp,
        Guid escrowTransactionId,
        string reason);

    /// <summary>Mark Created/Pending top-ups older than cutoff as Expired. Returns rows updated.</summary>
    Task<int> ExpireStaleTopUpsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}

public interface IPaymobClient
{
    bool IsConfigured { get; }
    Task<PaymobIntentionResult> CreateIntentionAsync(PaymobIntentionRequest request, CancellationToken ct = default);
    bool VerifyHmac(IReadOnlyDictionary<string, string?> obj, string receivedHmac);
    string BuildCheckoutUrl(string clientSecret);
}

public sealed class PaymobIntentionRequest
{
    public decimal AmountEgp { get; set; }
    public string SpecialReference { get; set; } = string.Empty;
    public string NotificationUrl { get; set; } = string.Empty;
    public string RedirectionUrl { get; set; } = string.Empty;
    public string Email { get; set; } = "buyer@nilechain.local";
    public string Phone { get; set; } = "+201000000000";
    public string FirstName { get; set; } = "Nile";
    public string LastName { get; set; } = "Chain";
}

public sealed class PaymobIntentionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? IntentionId { get; set; }
    public string? ClientSecret { get; set; }
    public string? OrderId { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? RawJson { get; set; }
}

public interface IWalletRepository
{
    Task<Domain.Entities.Wallet?> GetByOwnerAsync(WalletOwnerType ownerType, Guid ownerId, bool tracking = true);
    Task<Domain.Entities.Wallet?> GetByIdAsync(Guid walletId, bool tracking = true);
    Task AddAsync(Domain.Entities.Wallet wallet);
    Task AddLedgerAsync(Domain.Entities.WalletLedgerEntry entry);
    Task AddTopUpAsync(Domain.Entities.WalletTopUp topUp);
    Task<Domain.Entities.WalletTopUp?> GetTopUpByIdAsync(Guid topUpId, bool tracking = true);
    Task<Domain.Entities.WalletTopUp?> GetTopUpByIdempotencyAsync(string idempotencyKey, bool tracking = true);
    Task AddWithdrawalAsync(Domain.Entities.WalletWithdrawal withdrawal);
    Task<IReadOnlyList<Domain.Entities.WalletLedgerEntry>> GetRecentLedgerAsync(Guid walletId, int take = 30);
    Task<IReadOnlyList<Domain.Entities.WalletWithdrawal>> GetRecentWithdrawalsAsync(Guid walletId, int take = 20);
    Task<IReadOnlyList<Domain.Entities.WalletTopUp>> GetStalePendingTopUpsAsync(DateTime cutoffUtc, CancellationToken cancellationToken = default);
}
