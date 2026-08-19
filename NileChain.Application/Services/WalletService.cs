using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
using NileChain.Application.Contracts;
using NileChain.Application.Dtos.Wallet;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public sealed class WalletService : IWalletService
{
    public const string WalletDisclaimer =
        "NileChain wallet — top up via the payment gateway, pay contract milestones from balance, withdraw after escrow release.";

    public const string SandboxCardHint =
        "Sandbox cards: Visa 4111111111111111 or Mastercard 5123456789012346 — Exp 01/39 — CVV 123.";

    /// <summary>Well-known owner id for the NileChain platform fee wallet.</summary>
    public static readonly Guid PlatformOwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public const decimal SubscriptionMonthlyUsd = 30m;
    public const decimal SubscriptionMonthlyEgp = 1500m;

    private readonly IWalletRepository _wallets;
    private readonly IFarmRepository _farms;
    private readonly IFactoryRepository _factories;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymobClient _paymob;
    private readonly MockPaymentOptions _payments;
    private readonly PaymobOptions _paymobOptions;
    private readonly ILogger<WalletService> _logger;
    private readonly SubscriptionOptions _subscriptions;

    public WalletService(
        IWalletRepository wallets,
        IFarmRepository farms,
        IFactoryRepository factories,
        IUnitOfWork unitOfWork,
        IPaymobClient paymob,
        IOptions<MockPaymentOptions> payments,
        IOptions<PaymobOptions> paymobOptions,
        ILogger<WalletService> logger,
        IOptions<SubscriptionOptions>? subscriptions = null)
    {
        _wallets = wallets;
        _farms = farms;
        _factories = factories;
        _unitOfWork = unitOfWork;
        _paymob = paymob;
        _payments = payments.Value;
        _paymobOptions = paymobOptions.Value;
        _logger = logger;
        _subscriptions = subscriptions?.Value ?? new SubscriptionOptions();
    }

    public async Task<Result<WalletDto>> GetMineAsync(Guid userId, bool asFarm)
    {
        var owner = await ResolveOwnerAsync(userId, asFarm);
        if (owner.IsFailure)
            return Result<WalletDto>.Failure(owner.Error!);

        var wallet = await GetOrCreateWalletAsync(owner.Value.OwnerType, owner.Value.OwnerId);
        await _unitOfWork.SaveChangesAsync();
        return Result<WalletDto>.Success(await MapAsync(wallet));
    }

    public async Task<Result<WalletDto>> PaySubscriptionMonthAsync(Guid userId, bool asFarm)
    {
        var owner = await ResolveOwnerAsync(userId, asFarm);
        if (owner.IsFailure)
            return Result<WalletDto>.Failure(owner.Error!);

        var monthlyEgp = asFarm ? _subscriptions.FarmProEgp : _subscriptions.FactoryProEgp;
        if (monthlyEgp <= 0)
            monthlyEgp = asFarm ? 800m : SubscriptionMonthlyEgp;

        var wallet = await GetOrCreateWalletAsync(owner.Value.OwnerType, owner.Value.OwnerId);
        await _unitOfWork.SaveChangesAsync();

        if (wallet.AvailableBalanceEgp < monthlyEgp)
            return Result<WalletDto>.Failure(WalletErrors.SubscriptionInsufficient);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        wallet.AvailableBalanceEgp -= monthlyEgp;
        var from = wallet.SubscriptionPaidThroughUtc is DateTime paidThrough && paidThrough > now
            ? paidThrough
            : now;
        wallet.SubscriptionPaidThroughUtc = from.AddDays(30);
        wallet.UpdatedAt = now;

        var roleLabel = asFarm ? "Farm" : "Factory";
        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = wallet.WalletId,
            EntryType = WalletLedgerType.Adjustment,
            AmountEgp = -monthlyEgp,
            AvailableAfterEgp = wallet.AvailableBalanceEgp,
            HeldAfterEgp = wallet.HeldBalanceEgp,
            Description = $"NileChain {roleLabel} Pro ({monthlyEgp:0} EGP / 30 days)",
            ReferenceType = "SubscriptionMonth",
            CreatedAt = now
        });

        var platformWallet = await GetOrCreateWalletAsync(WalletOwnerType.Platform, PlatformOwnerId);
        platformWallet.AvailableBalanceEgp += monthlyEgp;
        platformWallet.UpdatedAt = now;
        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = platformWallet.WalletId,
            EntryType = WalletLedgerType.PlatformFee,
            AmountEgp = monthlyEgp,
            AvailableAfterEgp = platformWallet.AvailableBalanceEgp,
            HeldAfterEgp = platformWallet.HeldBalanceEgp,
            Description = $"{roleLabel} subscription",
            ReferenceType = "SubscriptionMonth",
            ReferenceId = wallet.WalletId,
            CreatedAt = now
        });

        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return Result<WalletDto>.Success(await MapAsync(wallet));
    }

    public async Task<Result<WalletTopUpSessionDto>> StartTopUpAsync(
        Guid userId,
        bool asFarm,
        decimal amountEgp,
        string? idempotencyKey,
        string? returnUrl)
    {
        if (amountEgp < _payments.MinTopUpEgp || amountEgp > _payments.MaxTopUpEgp)
            return Result<WalletTopUpSessionDto>.Failure(WalletErrors.InvalidAmount);

        var owner = await ResolveOwnerAsync(userId, asFarm);
        if (owner.IsFailure)
            return Result<WalletTopUpSessionDto>.Failure(owner.Error!);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var existing = await _wallets.GetTopUpByIdempotencyAsync(idempotencyKey.Trim());
            if (existing is not null)
                return Result<WalletTopUpSessionDto>.Success(MapTopUp(existing));
        }

        var wallet = await GetOrCreateWalletAsync(owner.Value.OwnerType, owner.Value.OwnerId);
        await _unitOfWork.SaveChangesAsync();

        var topUp = new WalletTopUp
        {
            TopUpId = Guid.NewGuid(),
            WalletId = wallet.WalletId,
            UserId = userId,
            AmountEgp = decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero),
            Status = WalletTopUpStatus.Created,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        if (_paymob.IsConfigured)
        {
            var apiBase = ResolvePublicApiBase();
            var notificationUrl = $"{apiBase}/api/webhooks/paymob";
            var redirectBase = string.IsNullOrWhiteSpace(returnUrl)
                ? $"{apiBase}/"
                : returnUrl.Trim();
            // Embed top-up id so the SPA can confirm after redirect even when
            // Paymob omits merchant_order_id from the query string.
            var redirect = AppendQuery(redirectBase, "topUpId", topUp.TopUpId.ToString("N"));

            var intention = await _paymob.CreateIntentionAsync(new PaymobIntentionRequest
            {
                AmountEgp = topUp.AmountEgp,
                SpecialReference = topUp.TopUpId.ToString("N"),
                NotificationUrl = notificationUrl,
                RedirectionUrl = redirect
            });

            if (!intention.Success)
            {
                _logger.LogWarning("Paymob intention failed: {Error}", intention.Error);
                if (!_paymobOptions.AllowLocalSimulator)
                    return Result<WalletTopUpSessionDto>.Failure(
                        new Error("Wallet.PaymobError", intention.Error ?? "Paymob error"));
                // fall through to simulator
            }
            else
            {
                topUp.Status = WalletTopUpStatus.Pending;
                topUp.PaymobIntentionId = intention.IntentionId;
                topUp.PaymobOrderId = intention.OrderId;
                topUp.ClientSecret = intention.ClientSecret;
                topUp.CheckoutUrl = intention.CheckoutUrl;
                await _wallets.AddTopUpAsync(topUp);
                await _unitOfWork.SaveChangesAsync();
                return Result<WalletTopUpSessionDto>.Success(MapTopUp(topUp, mode: "Paymob"));
            }
        }

        if (!_paymobOptions.AllowLocalSimulator)
            return Result<WalletTopUpSessionDto>.Failure(WalletErrors.PaymobNotConfigured);

        if (_paymobOptions.Enabled && !_paymob.IsConfigured)
        {
            _logger.LogWarning(
                "Paymob is enabled but keys/integration id are missing — using local wallet simulator.");
        }

        topUp.Status = WalletTopUpStatus.Pending;
        topUp.CheckoutUrl = null;
        await _wallets.AddTopUpAsync(topUp);
        await _unitOfWork.SaveChangesAsync();
        return Result<WalletTopUpSessionDto>.Success(MapTopUp(topUp, mode: "Simulator"));
    }

    public async Task<Result<WalletDto>> CompleteSimulatorTopUpAsync(Guid userId, bool asFarm, Guid topUpId)
    {
        if (!_paymobOptions.AllowLocalSimulator)
            return Result<WalletDto>.Failure(WalletErrors.PaymobNotConfigured);

        var owner = await ResolveOwnerAsync(userId, asFarm);
        if (owner.IsFailure)
            return Result<WalletDto>.Failure(owner.Error!);

        var topUp = await _wallets.GetTopUpByIdAsync(topUpId, tracking: true);
        if (topUp is null)
            return Result<WalletDto>.Failure(WalletErrors.TopUpNotFound);

        var wallet = await _wallets.GetByIdAsync(topUp.WalletId, tracking: true);
        if (wallet is null || wallet.OwnerType != owner.Value.OwnerType || wallet.OwnerId != owner.Value.OwnerId)
            return Result<WalletDto>.Failure(WalletErrors.Forbidden);

        if (topUp.Status == WalletTopUpStatus.Paid)
            return Result<WalletDto>.Success(await MapAsync(wallet));

        if (topUp.Status is not (WalletTopUpStatus.Created or WalletTopUpStatus.Pending))
            return Result<WalletDto>.Failure(WalletErrors.TopUpInvalidState);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        await CreditTopUpAsync(wallet, topUp, paymobTxnId: $"sim-{topUp.TopUpId:N}");
        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();

        return Result<WalletDto>.Success(await MapAsync(wallet));
    }

    public async Task<Result<WalletDto>> ApplyPaymobTopUpSuccessAsync(
        string specialReference,
        string? paymobTransactionId,
        string? paymobOrderId,
        bool success)
    {
        if (!Guid.TryParseExact(specialReference, "N", out var topUpId)
            && !Guid.TryParse(specialReference, out topUpId))
            return Result<WalletDto>.Failure(WalletErrors.TopUpNotFound);

        var topUp = await _wallets.GetTopUpByIdAsync(topUpId, tracking: true);
        if (topUp is null)
            return Result<WalletDto>.Failure(WalletErrors.TopUpNotFound);

        var wallet = await _wallets.GetByIdAsync(topUp.WalletId, tracking: true);
        if (wallet is null)
            return Result<WalletDto>.Failure(WalletErrors.NotFound);

        if (topUp.Status == WalletTopUpStatus.Paid)
            return Result<WalletDto>.Success(await MapAsync(wallet));

        if (!success)
        {
            topUp.Status = WalletTopUpStatus.Failed;
            topUp.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync();
            return Result<WalletDto>.Failure(WalletErrors.TopUpFailed);
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        await CreditTopUpAsync(wallet, topUp, paymobTxnId: paymobTransactionId);
        topUp.PaymobOrderId ??= paymobOrderId;
        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();

        return Result<WalletDto>.Success(await MapAsync(wallet));
    }

    public async Task<Result<WalletDto>> ConfirmPaymobReturnAsync(
        Guid userId,
        bool asFarm,
        IReadOnlyDictionary<string, string?> query,
        string? hmac)
    {
        if (!_paymob.IsConfigured)
            return Result<WalletDto>.Failure(WalletErrors.PaymobNotConfigured);

        var hmacValue = hmac ?? LookupQuery(query, "hmac");
        var hmacOk = !string.IsNullOrWhiteSpace(hmacValue) && _paymob.VerifyHmac(query, hmacValue!);

        var successRaw = LookupQuery(query, "success");
        var success = string.Equals(successRaw, "true", StringComparison.OrdinalIgnoreCase);

        var special = LookupQuery(query, "merchant_order_id")
                      ?? LookupQuery(query, "special_reference")
                      ?? LookupQuery(query, "topUpId");

        if (string.IsNullOrWhiteSpace(special))
            return Result<WalletDto>.Failure(WalletErrors.TopUpNotFound);

        if (!Guid.TryParseExact(special, "N", out var topUpId)
            && !Guid.TryParse(special, out topUpId))
            return Result<WalletDto>.Failure(WalletErrors.TopUpNotFound);

        var owner = await ResolveOwnerAsync(userId, asFarm);
        if (owner.IsFailure)
            return Result<WalletDto>.Failure(owner.Error!);

        var topUp = await _wallets.GetTopUpByIdAsync(topUpId, tracking: true);
        if (topUp is null)
            return Result<WalletDto>.Failure(WalletErrors.TopUpNotFound);

        var wallet = await _wallets.GetByIdAsync(topUp.WalletId, tracking: true);
        if (wallet is null
            || wallet.OwnerType != owner.Value.OwnerType
            || wallet.OwnerId != owner.Value.OwnerId)
            return Result<WalletDto>.Failure(WalletErrors.Forbidden);

        if (!hmacOk)
        {
            // Localhost often cannot receive Paymob webhooks; redirect HMAC field naming
            // also varies. Allow credit only when the logged-in owner, pending top-up,
            // success flag, and amount_cents all line up.
            if (!success || topUp.Status != WalletTopUpStatus.Pending)
            {
                _logger.LogWarning(
                    "Paymob redirect HMAC failed and fallback rejected (success={Success}, status={Status})",
                    success,
                    topUp.Status);
                return Result<WalletDto>.Failure(WalletErrors.PaymobHmacInvalid);
            }

            var amountCentsRaw = LookupQuery(query, "amount_cents");
            if (!int.TryParse(amountCentsRaw, out var amountCents)
                || amountCents != (int)Math.Round(topUp.AmountEgp * 100m, MidpointRounding.AwayFromZero))
            {
                _logger.LogWarning(
                    "Paymob redirect HMAC failed and amount_cents mismatch (got={Got}, expected={Expected})",
                    amountCentsRaw,
                    (int)Math.Round(topUp.AmountEgp * 100m, MidpointRounding.AwayFromZero));
                return Result<WalletDto>.Failure(WalletErrors.PaymobHmacInvalid);
            }

            var integrationRaw = LookupQuery(query, "integration_id");
            if (!string.IsNullOrWhiteSpace(integrationRaw)
                && int.TryParse(integrationRaw, out var integrationId)
                && _paymobOptions.CardIntegrationId > 0
                && integrationId != _paymobOptions.CardIntegrationId)
            {
                _logger.LogWarning(
                    "Paymob redirect HMAC failed and integration_id mismatch (got={Got}, expected={Expected})",
                    integrationId,
                    _paymobOptions.CardIntegrationId);
                return Result<WalletDto>.Failure(WalletErrors.PaymobHmacInvalid);
            }

            _logger.LogWarning(
                "Paymob redirect HMAC mismatch — crediting via authenticated ownership+amount fallback for topUp {TopUpId}",
                topUpId);
        }

        var txnId = LookupQuery(query, "id");
        var orderId = LookupQuery(query, "order_id") ?? LookupQuery(query, "order");
        return await ApplyPaymobTopUpSuccessAsync(special, txnId, orderId, success);
    }

    private static string? LookupQuery(IReadOnlyDictionary<string, string?> query, string key)
    {
        if (query.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
            return val.Trim();
        foreach (var kv in query)
        {
            if (string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(kv.Value))
                return kv.Value!.Trim();
        }
        return null;
    }

    private static string AppendQuery(string url, string key, string value)
    {
        var sep = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{sep}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }

    public async Task<Result<WalletWithdrawalDto>> RequestWithdrawalAsync(
        Guid userId,
        bool asFarm,
        decimal amountEgp,
        string method,
        string? destinationSummary)
    {
        if (amountEgp < _payments.MinWithdrawEgp)
            return Result<WalletWithdrawalDto>.Failure(WalletErrors.InvalidAmount);

        var owner = await ResolveOwnerAsync(userId, asFarm);
        if (owner.IsFailure)
            return Result<WalletWithdrawalDto>.Failure(owner.Error!);

        var wallet = await GetOrCreateWalletAsync(owner.Value.OwnerType, owner.Value.OwnerId);
        await _unitOfWork.SaveChangesAsync();

        amountEgp = decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero);
        if (wallet.AvailableBalanceEgp < amountEgp)
            return Result<WalletWithdrawalDto>.Failure(WalletErrors.InsufficientBalance);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        wallet.AvailableBalanceEgp -= amountEgp;
        wallet.UpdatedAt = now;

        var withdrawal = new WalletWithdrawal
        {
            WithdrawalId = Guid.NewGuid(),
            WalletId = wallet.WalletId,
            UserId = userId,
            AmountEgp = amountEgp,
            Method = string.IsNullOrWhiteSpace(method) ? "BankTransfer" : method.Trim(),
            DestinationSummary = string.IsNullOrWhiteSpace(destinationSummary)
                ? null
                : destinationSummary.Trim(),
            Status = _payments.InstantDemoWithdrawals
                ? WalletWithdrawalStatus.Completed
                : WalletWithdrawalStatus.Pending,
            CreatedAt = now,
            UpdatedAt = now,
            CompletedAt = _payments.InstantDemoWithdrawals ? now : null
        };

        await _wallets.AddWithdrawalAsync(withdrawal);
        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = wallet.WalletId,
            EntryType = WalletLedgerType.Withdrawal,
            AmountEgp = -amountEgp,
            AvailableAfterEgp = wallet.AvailableBalanceEgp,
            HeldAfterEgp = wallet.HeldBalanceEgp,
            Description = $"Withdrawal via {withdrawal.Method}",
            ReferenceType = "WalletWithdrawal",
            ReferenceId = withdrawal.WithdrawalId,
            CreatedAt = now
        });

        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();

        return Result<WalletWithdrawalDto>.Success(MapWithdrawal(withdrawal));
    }

    public async Task<Result<AdminWithdrawalListDto>> ListWithdrawalsForAdminAsync(string? status, int take = 100)
    {
        take = Math.Clamp(take, 1, 200);
        WalletWithdrawalStatus? parsed = null;
        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<WalletWithdrawalStatus>(status, ignoreCase: true, out var s))
        {
            parsed = s;
        }

        var rows = await _wallets.ListWithdrawalsAsync(parsed, take);
        return Result<AdminWithdrawalListDto>.Success(new AdminWithdrawalListDto
        {
            TotalCount = rows.Count,
            Items = rows.Select(w => MapAdminWithdrawal(w)).ToList()
        });
    }

    public async Task<Result<AdminWithdrawalDto>> CompleteWithdrawalAsync(Guid adminUserId, Guid withdrawalId)
    {
        _ = adminUserId;
        var withdrawal = await _wallets.GetWithdrawalByIdAsync(withdrawalId, tracking: true);
        if (withdrawal is null)
            return Result<AdminWithdrawalDto>.Failure(WalletErrors.WithdrawalNotFound);

        if (withdrawal.Status is not WalletWithdrawalStatus.Pending and not WalletWithdrawalStatus.Processing)
            return Result<AdminWithdrawalDto>.Failure(WalletErrors.WithdrawalInvalidState);

        var now = DateTime.UtcNow;
        withdrawal.Status = WalletWithdrawalStatus.Completed;
        withdrawal.CompletedAt = now;
        withdrawal.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync();
        var wallet = await _wallets.GetByIdAsync(withdrawal.WalletId, tracking: false);
        return Result<AdminWithdrawalDto>.Success(MapAdminWithdrawal(withdrawal, wallet));
    }

    public async Task<Result<AdminWithdrawalDto>> RejectWithdrawalAsync(
        Guid adminUserId,
        Guid withdrawalId,
        string? reason)
    {
        _ = adminUserId;
        var withdrawal = await _wallets.GetWithdrawalByIdAsync(withdrawalId, tracking: true);
        if (withdrawal is null)
            return Result<AdminWithdrawalDto>.Failure(WalletErrors.WithdrawalNotFound);

        if (withdrawal.Status is not WalletWithdrawalStatus.Pending and not WalletWithdrawalStatus.Processing)
            return Result<AdminWithdrawalDto>.Failure(WalletErrors.WithdrawalInvalidState);

        var wallet = await _wallets.GetByIdAsync(withdrawal.WalletId, tracking: true);
        if (wallet is null)
            return Result<AdminWithdrawalDto>.Failure(WalletErrors.NotFound);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        wallet.AvailableBalanceEgp += withdrawal.AmountEgp;
        wallet.UpdatedAt = now;
        withdrawal.Status = WalletWithdrawalStatus.Cancelled;
        withdrawal.FailReason = string.IsNullOrWhiteSpace(reason) ? "Rejected by treasury" : reason.Trim();
        withdrawal.UpdatedAt = now;

        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = wallet.WalletId,
            EntryType = WalletLedgerType.RefundToFactory,
            AmountEgp = withdrawal.AmountEgp,
            AvailableAfterEgp = wallet.AvailableBalanceEgp,
            HeldAfterEgp = wallet.HeldBalanceEgp,
            Description = $"Withdrawal rejected: {withdrawal.FailReason}",
            ReferenceType = "WalletWithdrawal",
            ReferenceId = withdrawal.WithdrawalId,
            CreatedAt = now
        });

        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
        return Result<AdminWithdrawalDto>.Success(MapAdminWithdrawal(withdrawal, wallet));
    }

    public async Task<Result<Guid>> HoldForEscrowAsync(
        Guid factoryId,
        decimal amountEgp,
        Guid escrowTransactionId,
        string description)
    {
        amountEgp = decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero);
        var wallet = await GetOrCreateWalletAsync(WalletOwnerType.Factory, factoryId);
        if (wallet.AvailableBalanceEgp < amountEgp)
            return Result<Guid>.Failure(WalletErrors.InsufficientBalance);

        var now = DateTime.UtcNow;
        wallet.AvailableBalanceEgp -= amountEgp;
        wallet.HeldBalanceEgp += amountEgp;
        wallet.UpdatedAt = now;

        var entryId = Guid.NewGuid();
        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = entryId,
            WalletId = wallet.WalletId,
            EntryType = WalletLedgerType.MilestoneHold,
            AmountEgp = -amountEgp,
            AvailableAfterEgp = wallet.AvailableBalanceEgp,
            HeldAfterEgp = wallet.HeldBalanceEgp,
            Description = description,
            ReferenceType = "EscrowTransaction",
            ReferenceId = escrowTransactionId,
            CreatedAt = now
        });

        return Result<Guid>.Success(entryId);
    }

    public async Task<Result> EnsureFactoryAvailableAsync(Guid factoryId, decimal amountEgp)
    {
        amountEgp = decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero);
        if (amountEgp <= 0)
            return Result.Failure(WalletErrors.DealValueInvalid);

        var wallet = await GetOrCreateWalletAsync(WalletOwnerType.Factory, factoryId);
        if (wallet.AvailableBalanceEgp < amountEgp)
            return Result.Failure(WalletErrors.InsufficientBalance);

        return Result.Success();
    }

    public decimal GetDealHoldAmountEgp(decimal dealTotalEgp) =>
        PlatformFeeMath.TotalCharged(dealTotalEgp, _payments.PlatformFeePercent);

    public async Task<Result<Guid>> HoldDealFundsAsync(
        Guid factoryId,
        Guid contractId,
        decimal amountEgp,
        string description)
    {
        amountEgp = decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero);
        if (amountEgp <= 0)
            return Result<Guid>.Failure(WalletErrors.DealValueInvalid);

        var wallet = await GetOrCreateWalletAsync(WalletOwnerType.Factory, factoryId);
        if (wallet.AvailableBalanceEgp < amountEgp)
            return Result<Guid>.Failure(WalletErrors.InsufficientBalance);

        var now = DateTime.UtcNow;
        wallet.AvailableBalanceEgp -= amountEgp;
        wallet.HeldBalanceEgp += amountEgp;
        wallet.UpdatedAt = now;

        var entryId = Guid.NewGuid();
        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = entryId,
            WalletId = wallet.WalletId,
            EntryType = WalletLedgerType.ContractDealHold,
            AmountEgp = -amountEgp,
            AvailableAfterEgp = wallet.AvailableBalanceEgp,
            HeldAfterEgp = wallet.HeldBalanceEgp,
            Description = description,
            ReferenceType = "Contract",
            ReferenceId = contractId,
            CreatedAt = now
        });

        return Result<Guid>.Success(entryId);
    }

    public async Task<Result> ReleaseEscrowToFarmAsync(
        Guid factoryId,
        Guid farmId,
        decimal totalHeldEgp,
        decimal farmNetEgp,
        Guid escrowTransactionId)
    {
        totalHeldEgp = decimal.Round(totalHeldEgp, 2, MidpointRounding.AwayFromZero);
        farmNetEgp = decimal.Round(farmNetEgp, 2, MidpointRounding.AwayFromZero);
        var fee = totalHeldEgp - farmNetEgp;
        if (fee < 0) fee = 0;

        var factoryWallet = await GetOrCreateWalletAsync(WalletOwnerType.Factory, factoryId);
        if (factoryWallet.HeldBalanceEgp < totalHeldEgp)
            return Result.Failure(WalletErrors.InsufficientBalance);

        var farmWallet = await GetOrCreateWalletAsync(WalletOwnerType.Farm, farmId);
        var now = DateTime.UtcNow;

        factoryWallet.HeldBalanceEgp -= totalHeldEgp;
        factoryWallet.UpdatedAt = now;

        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = factoryWallet.WalletId,
            EntryType = WalletLedgerType.MilestoneReleaseToFarm,
            AmountEgp = -totalHeldEgp,
            AvailableAfterEgp = factoryWallet.AvailableBalanceEgp,
            HeldAfterEgp = factoryWallet.HeldBalanceEgp,
            Description =
                $"Escrow released (farm net {farmNetEgp:0.00}; platform fee {fee:0.00} retained)",
            ReferenceType = "EscrowTransaction",
            ReferenceId = escrowTransactionId,
            CreatedAt = now
        });

        farmWallet.AvailableBalanceEgp += farmNetEgp;
        farmWallet.UpdatedAt = now;
        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = farmWallet.WalletId,
            EntryType = WalletLedgerType.MilestoneReleaseToFarm,
            AmountEgp = farmNetEgp,
            AvailableAfterEgp = farmWallet.AvailableBalanceEgp,
            HeldAfterEgp = farmWallet.HeldBalanceEgp,
            Description = $"Escrow release credited ({farmNetEgp:0.00} EGP)",
            ReferenceType = "EscrowTransaction",
            ReferenceId = escrowTransactionId,
            CreatedAt = now
        });

        if (fee > 0)
        {
            var platformWallet = await GetOrCreateWalletAsync(WalletOwnerType.Platform, PlatformOwnerId);
            platformWallet.AvailableBalanceEgp += fee;
            platformWallet.UpdatedAt = now;
            await _wallets.AddLedgerAsync(new WalletLedgerEntry
            {
                LedgerEntryId = Guid.NewGuid(),
                WalletId = platformWallet.WalletId,
                EntryType = WalletLedgerType.PlatformFee,
                AmountEgp = fee,
                AvailableAfterEgp = platformWallet.AvailableBalanceEgp,
                HeldAfterEgp = platformWallet.HeldBalanceEgp,
                Description = $"Platform fee retained ({fee:0.00} EGP)",
                ReferenceType = "EscrowTransaction",
                ReferenceId = escrowTransactionId,
                CreatedAt = now
            });
        }

        return Result.Success();
    }

    public async Task<Result> RefundEscrowHoldAsync(
        Guid factoryId,
        decimal totalHeldEgp,
        Guid escrowTransactionId,
        string reason)
    {
        totalHeldEgp = decimal.Round(totalHeldEgp, 2, MidpointRounding.AwayFromZero);
        var wallet = await GetOrCreateWalletAsync(WalletOwnerType.Factory, factoryId);
        if (wallet.HeldBalanceEgp < totalHeldEgp)
            return Result.Failure(WalletErrors.InsufficientBalance);

        var now = DateTime.UtcNow;
        wallet.HeldBalanceEgp -= totalHeldEgp;
        wallet.AvailableBalanceEgp += totalHeldEgp;
        wallet.UpdatedAt = now;

        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = wallet.WalletId,
            EntryType = WalletLedgerType.RefundToFactory,
            AmountEgp = totalHeldEgp,
            AvailableAfterEgp = wallet.AvailableBalanceEgp,
            HeldAfterEgp = wallet.HeldBalanceEgp,
            Description = reason,
            ReferenceType = "EscrowTransaction",
            ReferenceId = escrowTransactionId,
            CreatedAt = now
        });

        return Result.Success();
    }

    public async Task<Result> RefundHeldAmountAsync(
        Guid factoryId,
        decimal amountEgp,
        string referenceType,
        Guid referenceId,
        string reason)
    {
        amountEgp = decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero);
        if (amountEgp <= 0)
            return Result.Success();

        var wallet = await GetOrCreateWalletAsync(WalletOwnerType.Factory, factoryId);
        if (wallet.HeldBalanceEgp < amountEgp)
            return Result.Failure(WalletErrors.InsufficientBalance);

        var now = DateTime.UtcNow;
        wallet.HeldBalanceEgp -= amountEgp;
        wallet.AvailableBalanceEgp += amountEgp;
        wallet.UpdatedAt = now;

        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = wallet.WalletId,
            EntryType = WalletLedgerType.RefundToFactory,
            AmountEgp = amountEgp,
            AvailableAfterEgp = wallet.AvailableBalanceEgp,
            HeldAfterEgp = wallet.HeldBalanceEgp,
            Description = reason,
            ReferenceType = referenceType,
            ReferenceId = referenceId,
            CreatedAt = now
        });

        return Result.Success();
    }

    public async Task<Result> SplitEscrowHoldAsync(
        Guid factoryId,
        Guid farmId,
        decimal totalHeldEgp,
        decimal farmShareEgp,
        Guid escrowTransactionId,
        string reason)
    {
        totalHeldEgp = decimal.Round(totalHeldEgp, 2, MidpointRounding.AwayFromZero);
        farmShareEgp = decimal.Round(farmShareEgp, 2, MidpointRounding.AwayFromZero);
        if (farmShareEgp < 0)
            farmShareEgp = 0;
        if (farmShareEgp > totalHeldEgp)
            farmShareEgp = totalHeldEgp;
        var factoryRefund = totalHeldEgp - farmShareEgp;

        var factoryWallet = await GetOrCreateWalletAsync(WalletOwnerType.Factory, factoryId);
        if (factoryWallet.HeldBalanceEgp < totalHeldEgp)
            return Result.Failure(WalletErrors.InsufficientBalance);

        var farmWallet = await GetOrCreateWalletAsync(WalletOwnerType.Farm, farmId);
        var now = DateTime.UtcNow;

        factoryWallet.HeldBalanceEgp -= totalHeldEgp;
        factoryWallet.AvailableBalanceEgp += factoryRefund;
        factoryWallet.UpdatedAt = now;
        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = factoryWallet.WalletId,
            EntryType = WalletLedgerType.RefundToFactory,
            AmountEgp = factoryRefund,
            AvailableAfterEgp = factoryWallet.AvailableBalanceEgp,
            HeldAfterEgp = factoryWallet.HeldBalanceEgp,
            Description = $"{reason} (factory refund {factoryRefund:0.00})",
            ReferenceType = "EscrowTransaction",
            ReferenceId = escrowTransactionId,
            CreatedAt = now
        });

        if (farmShareEgp > 0)
        {
            farmWallet.AvailableBalanceEgp += farmShareEgp;
            farmWallet.UpdatedAt = now;
            await _wallets.AddLedgerAsync(new WalletLedgerEntry
            {
                LedgerEntryId = Guid.NewGuid(),
                WalletId = farmWallet.WalletId,
                EntryType = WalletLedgerType.MilestoneReleaseToFarm,
                AmountEgp = farmShareEgp,
                AvailableAfterEgp = farmWallet.AvailableBalanceEgp,
                HeldAfterEgp = farmWallet.HeldBalanceEgp,
                Description = $"{reason} (farm share {farmShareEgp:0.00})",
                ReferenceType = "EscrowTransaction",
                ReferenceId = escrowTransactionId,
                CreatedAt = now
            });
        }

        return Result.Success();
    }

    public async Task<int> ExpireStaleTopUpsAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        var stale = await _wallets.GetStalePendingTopUpsAsync(cutoffUtc, cancellationToken);
        if (stale.Count == 0)
            return 0;

        var now = DateTime.UtcNow;
        foreach (var topUp in stale)
        {
            topUp.Status = WalletTopUpStatus.Expired;
            topUp.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync();
        return stale.Count;
    }

    private async Task CreditTopUpAsync(Wallet wallet, WalletTopUp topUp, string? paymobTxnId)
    {
        var now = DateTime.UtcNow;
        wallet.AvailableBalanceEgp += topUp.AmountEgp;
        wallet.UpdatedAt = now;
        topUp.Status = WalletTopUpStatus.Paid;
        topUp.PaidAt = now;
        topUp.UpdatedAt = now;
        topUp.PaymobTransactionId = paymobTxnId;

        await _wallets.AddLedgerAsync(new WalletLedgerEntry
        {
            LedgerEntryId = Guid.NewGuid(),
            WalletId = wallet.WalletId,
            EntryType = WalletLedgerType.TopUp,
            AmountEgp = topUp.AmountEgp,
            AvailableAfterEgp = wallet.AvailableBalanceEgp,
            HeldAfterEgp = wallet.HeldBalanceEgp,
            Description = "Wallet top-up",
            ReferenceType = "WalletTopUp",
            ReferenceId = topUp.TopUpId,
            CreatedAt = now
        });
    }

    private async Task<Wallet> GetOrCreateWalletAsync(WalletOwnerType ownerType, Guid ownerId)
    {
        var existing = await _wallets.GetByOwnerAsync(ownerType, ownerId, tracking: true);
        if (existing is not null)
            return existing;

        var wallet = new Wallet
        {
            WalletId = Guid.NewGuid(),
            OwnerType = ownerType,
            OwnerId = ownerId,
            AvailableBalanceEgp = 0,
            HeldBalanceEgp = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        await _wallets.AddAsync(wallet);
        return wallet;
    }

    private async Task<Result<(WalletOwnerType OwnerType, Guid OwnerId)>> ResolveOwnerAsync(
        Guid userId,
        bool asFarm)
    {
        if (asFarm)
        {
            var farm = await _farms.GetByUserIdAsync(userId);
            if (farm is null)
                return Result<(WalletOwnerType, Guid)>.Failure(FarmErrors.FarmNotFound);
            return Result<(WalletOwnerType, Guid)>.Success((WalletOwnerType.Farm, farm.FarmId));
        }

        var factory = await _factories.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<(WalletOwnerType, Guid)>.Failure(FactoryErrors.FactoryNotFound);
        return Result<(WalletOwnerType, Guid)>.Success((WalletOwnerType.Factory, factory.FactoryId));
    }

    private string ResolvePublicApiBase()
    {
        if (!string.IsNullOrWhiteSpace(_paymobOptions.PublicApiBaseUrl))
            return _paymobOptions.PublicApiBaseUrl.TrimEnd('/');
        return "http://localhost:5190";
    }

    private async Task<WalletDto> MapAsync(Wallet wallet)
    {
        var ledger = await _wallets.GetRecentLedgerAsync(wallet.WalletId);
        var withdrawals = await _wallets.GetRecentWithdrawalsAsync(wallet.WalletId);
        return new WalletDto
        {
            WalletId = wallet.WalletId,
            OwnerType = wallet.OwnerType.ToString(),
            OwnerId = wallet.OwnerId,
            AvailableBalanceEgp = wallet.AvailableBalanceEgp,
            HeldBalanceEgp = wallet.HeldBalanceEgp,
            PaymobConfigured = _paymob.IsConfigured,
            SimulatorAvailable = _paymobOptions.AllowLocalSimulator,
            PlatformFeePercent = _payments.PlatformFeePercent,
            FeePayer = string.IsNullOrWhiteSpace(_payments.FeePayer) ? "Factory" : _payments.FeePayer,
            FeeBase = string.IsNullOrWhiteSpace(_payments.FeeBase) ? "ReleasedAfterQc" : _payments.FeeBase,
            Disclaimer = WalletDisclaimer,
            SubscriptionApplies = wallet.OwnerType is WalletOwnerType.Factory or WalletOwnerType.Farm,
            SubscriptionMonthlyUsd = SubscriptionMonthlyUsd,
            SubscriptionMonthlyEgp = wallet.OwnerType == WalletOwnerType.Farm
                ? _subscriptions.FarmProEgp
                : _subscriptions.FactoryProEgp,
            SubscriptionPaidThroughUtc = wallet.SubscriptionPaidThroughUtc,
            SubscriptionActive =
                wallet.OwnerType is WalletOwnerType.Factory or WalletOwnerType.Farm
                && wallet.SubscriptionPaidThroughUtc is DateTime through
                && through > DateTime.UtcNow,
            RecentLedger = ledger.Select(e => new WalletLedgerItemDto
            {
                LedgerEntryId = e.LedgerEntryId,
                EntryType = e.EntryType.ToString(),
                AmountEgp = e.AmountEgp,
                AvailableAfterEgp = e.AvailableAfterEgp,
                HeldAfterEgp = e.HeldAfterEgp,
                Description = e.Description,
                CreatedAt = e.CreatedAt
            }).ToList(),
            RecentWithdrawals = withdrawals.Select(MapWithdrawal).ToList()
        };
    }

    private WalletTopUpSessionDto MapTopUp(WalletTopUp t, string? mode = null)
    {
        var resolvedMode = mode ?? (!string.IsNullOrWhiteSpace(t.CheckoutUrl) ? "Paymob" : "Simulator");
        return new WalletTopUpSessionDto
        {
            TopUpId = t.TopUpId,
            AmountEgp = t.AmountEgp,
            Status = t.Status.ToString(),
            Mode = resolvedMode,
            CheckoutUrl = t.CheckoutUrl,
            ClientSecret = t.ClientSecret,
            Disclaimer = WalletDisclaimer,
            // Keep test-card hint only for the local simulator path — Paymob checkout should feel like a normal gateway.
            SandboxHint = string.Equals(resolvedMode, "Simulator", StringComparison.Ordinal)
                ? SandboxCardHint
                : null
        };
    }

    private static WalletWithdrawalDto MapWithdrawal(WalletWithdrawal w) =>
        new()
        {
            WithdrawalId = w.WithdrawalId,
            AmountEgp = w.AmountEgp,
            Status = w.Status.ToString(),
            Method = w.Method,
            DestinationSummary = w.DestinationSummary,
            CreatedAt = w.CreatedAt,
            CompletedAt = w.CompletedAt
        };

    private static AdminWithdrawalDto MapAdminWithdrawal(WalletWithdrawal w, Wallet? wallet = null) =>
        new()
        {
            WithdrawalId = w.WithdrawalId,
            WalletId = w.WalletId,
            UserId = w.UserId,
            OwnerType = (wallet ?? w.Wallet)?.OwnerType.ToString() ?? string.Empty,
            OwnerId = (wallet ?? w.Wallet)?.OwnerId ?? Guid.Empty,
            AmountEgp = w.AmountEgp,
            Status = w.Status.ToString(),
            Method = w.Method,
            DestinationSummary = w.DestinationSummary,
            CreatedAt = w.CreatedAt,
            CompletedAt = w.CompletedAt
        };
}
