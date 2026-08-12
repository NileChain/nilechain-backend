using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
using NileChain.Application.Contracts;
using NileChain.Application.Dtos.Payment;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public sealed class MockEscrowPaymentService : IMockEscrowPaymentService
{
    public const string MockDisclaimer =
        "Demo mock payment — no real money is charged. NileChain simulates escrow hold/release and a platform service fee.";

    public const string WalletDisclaimer =
        "Pay from NileChain wallet (top up via Paymob sandbox). Milestone + platform fee held in escrow; farm receives net in their wallet on release.";

    private readonly IEscrowTransactionRepository _escrows;
    private readonly IPaymentMilestoneRepository _milestones;
    private readonly IDisputeRepository _disputes;
    private readonly IFulfillmentRepository _fulfillments;
    private readonly IFarmRepository _farms;
    private readonly IFactoryRepository _factories;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPaymentMilestoneService _paymentMilestones;
    private readonly IWalletService _wallets;
    private readonly MockPaymentOptions _options;
    private readonly ILogger<MockEscrowPaymentService> _logger;

    public MockEscrowPaymentService(
        IEscrowTransactionRepository escrows,
        IPaymentMilestoneRepository milestones,
        IDisputeRepository disputes,
        IFulfillmentRepository fulfillments,
        IFarmRepository farms,
        IFactoryRepository factories,
        IRepository<Contract> contracts,
        IRepository<Notification> notifications,
        IUnitOfWork unitOfWork,
        IPaymentMilestoneService paymentMilestones,
        IWalletService wallets,
        IOptions<MockPaymentOptions> options,
        ILogger<MockEscrowPaymentService> logger)
    {
        _escrows = escrows;
        _milestones = milestones;
        _disputes = disputes;
        _fulfillments = fulfillments;
        _farms = farms;
        _factories = factories;
        _contracts = contracts;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _paymentMilestones = paymentMilestones;
        _wallets = wallets;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsMockGatewayEnabled => _options.MockGatewayEnabled;

    public decimal PlatformFeePercent =>
        _options.PlatformFeePercent < 0 ? 0 : Math.Min(_options.PlatformFeePercent, 30m);

    private bool UseWallet => _options.WalletEnabled;

    private string ActiveDisclaimer => UseWallet ? WalletDisclaimer : MockDisclaimer;

    private string ActiveGateway => UseWallet ? "Wallet" : "Mock";

    public async Task<Result<MockEscrowSessionDto>> CreateSessionAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid transactionId,
        string? idempotencyKey = null)
    {
        if (!_options.MockGatewayEnabled)
            return Result<MockEscrowSessionDto>.Failure(MockEscrowErrors.GatewayDisabled);

        var access = await EnsureFactoryContractAsync(factoryUserId, contractId);
        if (access.IsFailure)
            return Result<MockEscrowSessionDto>.Failure(access.Error!);

        var contract = access.Value;
        if (contract.Status != ContractStatus.Signed || !contract.IsFullySigned)
            return Result<MockEscrowSessionDto>.Failure(PaymentMilestoneErrors.ContractNotSigned);

        if (await _disputes.HasActiveDisputeAsync(contractId))
            return Result<MockEscrowSessionDto>.Failure(PaymentMilestoneErrors.FrozenByDispute);

        var milestone = await _milestones.GetByIdAsync(transactionId, includeEvents: false);
        if (milestone is null || milestone.ContractId != contractId)
            return Result<MockEscrowSessionDto>.Failure(PaymentMilestoneErrors.MilestoneNotFound);

        if (milestone.Status != TransactionStatus.Pending)
            return Result<MockEscrowSessionDto>.Failure(MockEscrowErrors.MilestoneNotPayable);

        var existing = await _escrows.GetActiveByTransactionIdAsync(transactionId);
        if (existing is not null)
            return Result<MockEscrowSessionDto>.Success(MapSession(existing, milestone.Label));

        var farm = contract.FarmMatch?.Farm;
        var factory = contract.FarmMatch?.SupplyRequest?.Factory;
        if (farm is null || factory is null)
            return Result<MockEscrowSessionDto>.Failure(PaymentMilestoneErrors.ContractNotFound);

        var feePercent = PlatformFeePercent;
        var fee = decimal.Round(milestone.Amount * feePercent / 100m, 2, MidpointRounding.AwayFromZero);
        var total = milestone.Amount + fee;
        var now = DateTime.UtcNow;

        var escrow = new EscrowTransaction
        {
            EscrowTransactionId = Guid.NewGuid(),
            ContractId = contractId,
            TransactionId = transactionId,
            FactoryId = factory.FactoryId,
            FarmId = farm.FarmId,
            MilestoneAmountEgp = milestone.Amount,
            PlatformFeePercent = feePercent,
            PlatformFeeEgp = fee,
            TotalChargedEgp = total,
            FarmNetEgp = milestone.Amount,
            Currency = "EGP",
            Status = EscrowStatus.Created,
            IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim(),
            Gateway = ActiveGateway,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _escrows.AddAsync(escrow);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation(
            "Mock escrow session created EscrowId={EscrowId} Milestone={Amount} Fee={Fee} Total={Total}",
            escrow.EscrowTransactionId,
            escrow.MilestoneAmountEgp,
            escrow.PlatformFeeEgp,
            escrow.TotalChargedEgp);

        return Result<MockEscrowSessionDto>.Success(MapSession(escrow, milestone.Label));
    }

    public async Task<Result<PaymentMilestoneScheduleDto>> ConfirmPaidAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid escrowTransactionId)
    {
        if (!_options.MockGatewayEnabled)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.GatewayDisabled);

        var access = await EnsureFactoryContractAsync(factoryUserId, contractId);
        if (access.IsFailure)
            return Result<PaymentMilestoneScheduleDto>.Failure(access.Error!);

        var contract = access.Value;
        if (await _disputes.HasActiveDisputeAsync(contractId))
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.FrozenByDispute);

        var escrow = await _escrows.GetByIdAsync(escrowTransactionId, tracking: true);
        if (escrow is null || escrow.ContractId != contractId)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.NotFound);

        if (escrow.Status is EscrowStatus.Held or EscrowStatus.Released)
            return await _paymentMilestones.GetByContractAsync(factoryUserId, contractId, asFarm: false);

        if (escrow.Status is not (EscrowStatus.Created or EscrowStatus.Pending))
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.InvalidState);

        var milestone = await _milestones.GetByIdAsync(escrow.TransactionId, includeEvents: false);
        if (milestone is null || milestone.Status != TransactionStatus.Pending)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.MilestoneNotPayable);

        await using var dbTx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;

        if (UseWallet)
        {
            // Full deal was already held at contract full-sign — do not debit Available again.
            if (!contract.HasDealFundsHeld)
            {
                var hold = await _wallets.HoldForEscrowAsync(
                    escrow.FactoryId,
                    escrow.TotalChargedEgp,
                    escrow.EscrowTransactionId,
                    $"Escrow hold for '{milestone.Label}'");
                if (hold.IsFailure)
                    return Result<PaymentMilestoneScheduleDto>.Failure(hold.Error!);

                escrow.FundingLedgerEntryId = hold.Value;
            }

            escrow.UpdatedAt = now;
        }

        var heldOk = await _escrows.TryAtomicStatusAsync(
            escrow.EscrowTransactionId,
            escrow.Status,
            EscrowStatus.Held,
            now);
        if (!heldOk)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.Conflict);

        var milestoneOk = await _milestones.TryAtomicTransitionAsync(
            escrow.TransactionId,
            TransactionStatus.Pending,
            TransactionStatus.EscrowHeld,
            now,
            requireNoActiveDispute: true);
        if (!milestoneOk)
        {
            var frozen = await _disputes.HasActiveDisputeAsync(contractId);
            return Result<PaymentMilestoneScheduleDto>.Failure(
                frozen ? PaymentMilestoneErrors.FrozenByDispute : MockEscrowErrors.Conflict);
        }

        await _milestones.AddEventAsync(new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            TransactionId = escrow.TransactionId,
            FromStatus = TransactionStatus.Pending,
            ToStatus = TransactionStatus.EscrowHeld,
            ActorUserId = factoryUserId,
            Note = UseWallet
                ? $"Wallet pay held. Debited {escrow.TotalChargedEgp:0.00} EGP " +
                  $"(milestone {escrow.MilestoneAmountEgp:0.00} + platform fee {escrow.PlatformFeeEgp:0.00} @ {escrow.PlatformFeePercent:0.##}%)."
                : $"Mock pay held. Charged {escrow.TotalChargedEgp:0.00} EGP " +
                  $"(milestone {escrow.MilestoneAmountEgp:0.00} + platform fee {escrow.PlatformFeeEgp:0.00} @ {escrow.PlatformFeePercent:0.##}%). Demo only.",
            CreatedAt = now
        });

        await NotifyAsync(
            contract,
            factoryUserId,
            actorIsFarm: false,
            title: UseWallet ? "Payment held in escrow" : "Mock payment held in escrow",
            type: "EscrowHeld",
            message: UseWallet
                ? $"Factory paid '{milestone.Label}' from NileChain wallet. " +
                  $"Platform fee {escrow.PlatformFeeEgp:0.00} EGP. Funds held until release."
                : $"Factory paid '{milestone.Label}' via demo escrow. " +
                  $"Platform fee {escrow.PlatformFeeEgp:0.00} EGP. Funds simulated as held until release.");

        await _unitOfWork.SaveChangesAsync();
        await dbTx.CommitAsync();

        return await _paymentMilestones.GetByContractAsync(factoryUserId, contractId, asFarm: false);
    }

    public async Task<Result<PaymentMilestoneScheduleDto>> ConfirmReleaseAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid escrowTransactionId)
    {
        if (!_options.MockGatewayEnabled)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.GatewayDisabled);

        var access = await EnsureFactoryContractAsync(factoryUserId, contractId);
        if (access.IsFailure)
            return Result<PaymentMilestoneScheduleDto>.Failure(access.Error!);

        var contract = access.Value;
        if (await _disputes.HasActiveDisputeAsync(contractId))
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.FrozenByDispute);

        var escrow = await _escrows.GetByIdAsync(escrowTransactionId, tracking: false);
        if (escrow is null || escrow.ContractId != contractId)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.NotFound);

        if (escrow.Status == EscrowStatus.Released)
            return await _paymentMilestones.GetByContractAsync(factoryUserId, contractId, asFarm: false);

        if (escrow.Status != EscrowStatus.Held)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.InvalidState);

        var milestone = await _milestones.GetByIdAsync(escrow.TransactionId, includeEvents: false);
        if (milestone is null || milestone.Status != TransactionStatus.EscrowHeld)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.InvalidState);

        if (!await CanReleaseAsync(contractId, milestone))
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.ReleaseNotReady);

        await using var dbTx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        const string reason = "Factory confirmed escrow release";

        if (UseWallet)
        {
            var releaseWallet = await _wallets.ReleaseEscrowToFarmAsync(
                escrow.FactoryId,
                escrow.FarmId,
                escrow.TotalChargedEgp,
                escrow.FarmNetEgp,
                escrow.EscrowTransactionId);
            if (releaseWallet.IsFailure)
                return Result<PaymentMilestoneScheduleDto>.Failure(releaseWallet.Error!);

            await ReduceFundsHeldAsync(contract, escrow.TotalChargedEgp);
        }

        var releaseOk = await _escrows.TryAtomicStatusAsync(
            escrow.EscrowTransactionId,
            EscrowStatus.Held,
            EscrowStatus.Released,
            now,
            reason);
        if (!releaseOk)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.Conflict);

        var milestoneOk = await _milestones.TryAtomicTransitionAsync(
            escrow.TransactionId,
            TransactionStatus.EscrowHeld,
            TransactionStatus.Completed,
            now,
            requireNoActiveDispute: true);
        if (!milestoneOk)
        {
            var frozen = await _disputes.HasActiveDisputeAsync(contractId);
            return Result<PaymentMilestoneScheduleDto>.Failure(
                frozen ? PaymentMilestoneErrors.FrozenByDispute : MockEscrowErrors.Conflict);
        }

        await _milestones.AddEventAsync(new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            TransactionId = escrow.TransactionId,
            FromStatus = TransactionStatus.EscrowHeld,
            ToStatus = TransactionStatus.Completed,
            ActorUserId = factoryUserId,
            Note =
                $"Escrow released. Farm net {escrow.FarmNetEgp:0.00} EGP; " +
                $"platform fee revenue {escrow.PlatformFeeEgp:0.00} EGP." +
                (UseWallet ? " Credited to farm wallet." : " Demo only."),
            CreatedAt = now
        });

        await NotifyAsync(
            contract,
            factoryUserId,
            actorIsFarm: false,
            title: "Escrow released",
            type: "EscrowReleased",
            message:
                $"Escrow for '{milestone.Label}' released. Farm net {escrow.FarmNetEgp:0.00} EGP" +
                (UseWallet ? " credited to farm wallet. " : ". ") +
                $"NileChain platform fee {escrow.PlatformFeeEgp:0.00} EGP.");

        await _unitOfWork.SaveChangesAsync();
        await dbTx.CommitAsync();

        return await _paymentMilestones.GetByContractAsync(factoryUserId, contractId, asFarm: false);
    }

    public async Task<Result<PaymentMilestoneScheduleDto>> AdminRefundAsync(
        Guid adminUserId,
        Guid escrowTransactionId,
        string reason)
    {
        var escrow = await _escrows.GetByIdAsync(escrowTransactionId, tracking: false);
        if (escrow is null)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.NotFound);

        if (escrow.Status != EscrowStatus.Held)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.InvalidState);

        var contract = await _contracts.GetByIdAsync(escrow.ContractId);
        if (contract is null)
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.ContractNotFound);

        await using var dbTx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(reason) ? "Admin escrow refund" : reason.Trim();

        if (UseWallet)
        {
            var refundWallet = await _wallets.RefundEscrowHoldAsync(
                escrow.FactoryId,
                escrow.TotalChargedEgp,
                escrow.EscrowTransactionId,
                note);
            if (refundWallet.IsFailure)
                return Result<PaymentMilestoneScheduleDto>.Failure(refundWallet.Error!);

            await ReduceFundsHeldAsync(contract, escrow.TotalChargedEgp);
        }

        var refundOk = await _escrows.TryAtomicStatusAsync(
            escrow.EscrowTransactionId,
            EscrowStatus.Held,
            EscrowStatus.Refunded,
            now,
            note);
        if (!refundOk)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.Conflict);

        var milestoneOk = await _milestones.TryAtomicTransitionAsync(
            escrow.TransactionId,
            TransactionStatus.EscrowHeld,
            TransactionStatus.Refunded,
            now);
        if (!milestoneOk)
            return Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.Conflict);

        await _milestones.AddEventAsync(new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            TransactionId = escrow.TransactionId,
            FromStatus = TransactionStatus.EscrowHeld,
            ToStatus = TransactionStatus.Refunded,
            ActorUserId = adminUserId,
            Note = $"Escrow refunded: {note}",
            CreatedAt = now
        });

        await _unitOfWork.SaveChangesAsync();
        await dbTx.CommitAsync();

        // Admin may not be a party — return factory schedule shape via Get using factory user if available.
        var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
        if (factoryUserId is null || factoryUserId == Guid.Empty)
        {
            return Result<PaymentMilestoneScheduleDto>.Success(new PaymentMilestoneScheduleDto
            {
                ContractId = escrow.ContractId,
                Disclaimer = ActiveDisclaimer,
                Milestones = []
            });
        }

        return await _paymentMilestones.GetByContractAsync(factoryUserId.Value, escrow.ContractId, asFarm: false);
    }

    public async Task<Result> AdminRefundHeldForContractAsync(
        Guid adminUserId,
        Guid contractId,
        string reason)
    {
        var rows = await _escrows.GetByContractIdAsync(contractId);
        var held = rows.Where(e => e.Status == EscrowStatus.Held).ToList();
        if (held.Count == 0)
            return Result.Success();

        foreach (var escrow in held)
        {
            var refund = await AdminRefundAsync(adminUserId, escrow.EscrowTransactionId, reason);
            if (refund.IsFailure)
                return Result.Failure(refund.Error!);
        }

        return Result.Success();
    }

    public async Task<Result<IReadOnlyList<EscrowTransactionDto>>> ListForContractAsync(
        Guid userId,
        Guid contractId,
        bool asFarm)
    {
        Result<Contract> access = asFarm
            ? await EnsureFarmContractAsync(userId, contractId)
            : await EnsureFactoryContractAsync(userId, contractId);
        if (access.IsFailure)
            return Result<IReadOnlyList<EscrowTransactionDto>>.Failure(access.Error!);

        var rows = await _escrows.GetByContractIdAsync(contractId);
        IReadOnlyList<EscrowTransactionDto> dtos = rows.Select(MapEscrow).ToList();
        return Result<IReadOnlyList<EscrowTransactionDto>>.Success(dtos);
    }

    public async Task<Result> UnwindSignedDealAsync(Guid contractId, Guid actorUserId, string reason)
    {
        var contract = await _contracts.GetByIdAsync(contractId);
        if (contract is null)
            return Result.Failure(PaymentMilestoneErrors.ContractNotFound);

        if (contract.Status != ContractStatus.Signed)
            return Result.Failure(PaymentMilestoneErrors.ContractNotSigned);

        if (await _disputes.HasActiveDisputeAsync(contractId))
            return Result.Failure(MockEscrowErrors.UnwindBlockedByDispute);

        var fulfillment = await _fulfillments.GetByContractIdAsync(contractId, includeEvents: false);
        if (fulfillment is not null
            && fulfillment.Status is not (FulfillmentStatus.Planned or FulfillmentStatus.Shipped))
        {
            return Result.Failure(MockEscrowErrors.CannotUnwindAfterReceive);
        }

        await using var dbTx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(reason) ? "Signed deal unwind" : reason.Trim();

        var remaining = contract.FundsHeldEgp ?? 0m;
        var escrowsForFactory = await _escrows.GetByContractIdAsync(contractId);
        var factoryId = fulfillment?.Contract?.FarmMatch?.SupplyRequest?.FactoryId
                        ?? contract.FarmMatch?.SupplyRequest?.FactoryId
                        ?? escrowsForFactory.FirstOrDefault()?.FactoryId;
        if (UseWallet && remaining > 0 && factoryId is Guid fid && fid != Guid.Empty)
        {
            var refund = await _wallets.RefundHeldAmountAsync(
                fid, remaining, "Contract", contractId, note);
            if (refund.IsFailure)
                return Result.Failure(refund.Error!);
        }

        contract.FundsHeldEgp = 0;
        _contracts.Update(contract);

        var escrows = escrowsForFactory;
        foreach (var escrow in escrows)
        {
            if (escrow.Status is EscrowStatus.Held)
            {
                await _escrows.TryAtomicStatusAsync(
                    escrow.EscrowTransactionId,
                    EscrowStatus.Held,
                    EscrowStatus.Refunded,
                    now,
                    note);
                await _milestones.TryAtomicTransitionAsync(
                    escrow.TransactionId,
                    TransactionStatus.EscrowHeld,
                    TransactionStatus.Refunded,
                    now);
            }
            else if (escrow.Status is EscrowStatus.Created or EscrowStatus.Pending)
            {
                await _escrows.TryAtomicStatusAsync(
                    escrow.EscrowTransactionId,
                    escrow.Status,
                    EscrowStatus.Failed,
                    now,
                    note);
            }
        }

        var milestones = await _milestones.GetByContractIdAsync(contractId, includeEvents: false);
        foreach (var milestone in milestones.Where(t =>
                     t.Status is TransactionStatus.Pending or TransactionStatus.MarkedPaid))
        {
            var ok = await _milestones.TryAtomicTransitionAsync(
                milestone.TransactionId, milestone.Status, TransactionStatus.Voided, now);
            if (!ok)
                continue;
            await _milestones.AddEventAsync(new TransactionEvent
            {
                EventId = Guid.NewGuid(),
                TransactionId = milestone.TransactionId,
                FromStatus = milestone.Status,
                ToStatus = TransactionStatus.Voided,
                ActorUserId = actorUserId,
                Note = note,
                CreatedAt = now
            });
        }

        await _unitOfWork.SaveChangesAsync();
        await dbTx.CommitAsync();
        return Result.Success();
    }

    public async Task<Result> RefundLeftoverDealHoldAsync(Guid contractId, Guid actorUserId, string reason)
    {
        var contract = await _contracts.GetByIdAsync(contractId);
        if (contract is null)
            return Result.Failure(PaymentMilestoneErrors.ContractNotFound);

        var now = DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(reason) ? "Gate reject leftover hold" : reason.Trim();

        var remaining = contract.FundsHeldEgp ?? 0m;
        var escrowsForFactory = await _escrows.GetByContractIdAsync(contractId);
        var fulfillment = await _fulfillments.GetByContractIdAsync(contractId, includeEvents: false);
        var factoryId = fulfillment?.Contract?.FarmMatch?.SupplyRequest?.FactoryId
                        ?? contract.FarmMatch?.SupplyRequest?.FactoryId
                        ?? escrowsForFactory.FirstOrDefault()?.FactoryId;
        if (UseWallet && remaining > 0 && factoryId is Guid fid && fid != Guid.Empty)
        {
            var refund = await _wallets.RefundHeldAmountAsync(
                fid, remaining, "Contract", contractId, note);
            if (refund.IsFailure)
                return Result.Failure(refund.Error!);
        }

        contract.FundsHeldEgp = 0;
        _contracts.Update(contract);

        foreach (var escrow in escrowsForFactory)
        {
            if (escrow.Status is EscrowStatus.Held)
            {
                await _escrows.TryAtomicStatusAsync(
                    escrow.EscrowTransactionId,
                    EscrowStatus.Held,
                    EscrowStatus.Refunded,
                    now,
                    note);
                await _milestones.TryAtomicTransitionAsync(
                    escrow.TransactionId,
                    TransactionStatus.EscrowHeld,
                    TransactionStatus.Refunded,
                    now);
            }
            else if (escrow.Status is EscrowStatus.Created or EscrowStatus.Pending)
            {
                await _escrows.TryAtomicStatusAsync(
                    escrow.EscrowTransactionId,
                    escrow.Status,
                    EscrowStatus.Failed,
                    now,
                    note);
            }
        }

        var milestones = await _milestones.GetByContractIdAsync(contractId, includeEvents: false);
        foreach (var milestone in milestones.Where(t =>
                     t.Status is TransactionStatus.Pending or TransactionStatus.MarkedPaid))
        {
            var ok = await _milestones.TryAtomicTransitionAsync(
                milestone.TransactionId, milestone.Status, TransactionStatus.Voided, now);
            if (!ok)
                continue;
            await _milestones.AddEventAsync(new TransactionEvent
            {
                EventId = Guid.NewGuid(),
                TransactionId = milestone.TransactionId,
                FromStatus = milestone.Status,
                ToStatus = TransactionStatus.Voided,
                ActorUserId = actorUserId,
                Note = note,
                CreatedAt = now
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> ApplyQcAmountAdjustmentAsync(
        Guid contractId,
        Guid transactionId,
        decimal previousAmount,
        decimal newAmount)
    {
        previousAmount = decimal.Round(previousAmount, 2, MidpointRounding.AwayFromZero);
        newAmount = decimal.Round(newAmount, 2, MidpointRounding.AwayFromZero);
        if (newAmount >= previousAmount)
            return Result.Success();

        var contract = await _contracts.GetByIdAsync(contractId);
        if (contract is null)
            return Result.Failure(PaymentMilestoneErrors.ContractNotFound);

        var oldTotal = PlatformFeeMath.TotalCharged(previousAmount, PlatformFeePercent);
        var newTotal = PlatformFeeMath.TotalCharged(newAmount, PlatformFeePercent);
        var refund = oldTotal - newTotal;
        if (refund <= 0)
            return Result.Success();

        var factoryId = contract.FarmMatch?.SupplyRequest?.FactoryId;
        if (factoryId is null || factoryId == Guid.Empty)
        {
            var any = (await _escrows.GetByContractIdAsync(contractId)).FirstOrDefault();
            factoryId = any?.FactoryId;
        }

        if (UseWallet && factoryId is Guid fid && fid != Guid.Empty)
        {
            var walletRefund = await _wallets.RefundHeldAmountAsync(
                fid,
                refund,
                "Contract",
                contractId,
                $"QC amount adjustment ({previousAmount:0.00} → {newAmount:0.00})");
            if (walletRefund.IsFailure)
                return Result.Failure(walletRefund.Error!);
        }

        await ReduceFundsHeldAsync(contract, refund);

        var escrow = await _escrows.GetActiveByTransactionIdAsync(transactionId);
        if (escrow is not null
            && escrow.Status is EscrowStatus.Created or EscrowStatus.Pending or EscrowStatus.Held)
        {
            var tracked = await _escrows.GetByIdAsync(escrow.EscrowTransactionId, tracking: true);
            if (tracked is not null)
            {
                tracked.MilestoneAmountEgp = newAmount;
                tracked.FarmNetEgp = newAmount;
                tracked.PlatformFeeEgp = PlatformFeeMath.FeeOn(newAmount, PlatformFeePercent);
                tracked.TotalChargedEgp = newTotal;
                tracked.UpdatedAt = DateTime.UtcNow;
                await _escrows.UpdateAsync(tracked);
            }
        }

        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> SettleDisputeOutcomeAsync(
        Guid contractId,
        Guid actorUserId,
        string outcomeFavor,
        string reason)
    {
        if (!Enum.TryParse<DisputeOutcomeFavor>(outcomeFavor, ignoreCase: true, out var favor)
            || favor is DisputeOutcomeFavor.None)
        {
            return Result.Failure(DisputeErrors.OutcomeFavorRequired);
        }

        var contract = await _contracts.GetByIdAsync(contractId);
        if (contract is null)
            return Result.Failure(PaymentMilestoneErrors.ContractNotFound);

        var now = DateTime.UtcNow;
        var note = string.IsNullOrWhiteSpace(reason) ? $"Dispute settled ({favor})" : reason.Trim();
        var escrows = await _escrows.GetByContractIdAsync(contractId);

        foreach (var escrow in escrows.Where(e => e.Status == EscrowStatus.Held))
        {
            if (!UseWallet)
            {
                await _escrows.TryAtomicStatusAsync(
                    escrow.EscrowTransactionId, EscrowStatus.Held, EscrowStatus.Refunded, now, note);
                continue;
            }

            if (favor == DisputeOutcomeFavor.Farm)
            {
                var release = await _wallets.ReleaseEscrowToFarmAsync(
                    escrow.FactoryId,
                    escrow.FarmId,
                    escrow.TotalChargedEgp,
                    escrow.FarmNetEgp,
                    escrow.EscrowTransactionId);
                if (release.IsFailure)
                    return Result.Failure(release.Error!);

                await _escrows.TryAtomicStatusAsync(
                    escrow.EscrowTransactionId, EscrowStatus.Held, EscrowStatus.Released, now, note);
                await _milestones.TryAtomicTransitionAsync(
                    escrow.TransactionId, TransactionStatus.EscrowHeld, TransactionStatus.Completed, now);
                await ReduceFundsHeldAsync(contract, escrow.TotalChargedEgp);
            }
            else if (favor == DisputeOutcomeFavor.Split)
            {
                var farmShare = decimal.Round(escrow.FarmNetEgp / 2m, 2, MidpointRounding.AwayFromZero);
                var split = await _wallets.SplitEscrowHoldAsync(
                    escrow.FactoryId,
                    escrow.FarmId,
                    escrow.TotalChargedEgp,
                    farmShare,
                    escrow.EscrowTransactionId,
                    note);
                if (split.IsFailure)
                    return Result.Failure(split.Error!);

                await _escrows.TryAtomicStatusAsync(
                    escrow.EscrowTransactionId, EscrowStatus.Held, EscrowStatus.Released, now, note);
                await _milestones.TryAtomicTransitionAsync(
                    escrow.TransactionId, TransactionStatus.EscrowHeld, TransactionStatus.Completed, now);
                await ReduceFundsHeldAsync(contract, escrow.TotalChargedEgp);
            }
            else
            {
                await _escrows.TryAtomicStatusAsync(
                    escrow.EscrowTransactionId, EscrowStatus.Held, EscrowStatus.Refunded, now, note);
                await _milestones.TryAtomicTransitionAsync(
                    escrow.TransactionId, TransactionStatus.EscrowHeld, TransactionStatus.Refunded, now);
            }
        }

        var leftover = contract.FundsHeldEgp ?? 0m;
        var factoryId = contract.FarmMatch?.SupplyRequest?.FactoryId
                        ?? escrows.FirstOrDefault()?.FactoryId;
        if (UseWallet && leftover > 0 && factoryId is Guid fid && fid != Guid.Empty)
        {
            var leftoverRefund = await _wallets.RefundHeldAmountAsync(
                fid, leftover, "Contract", contractId, $"{note} — leftover deal hold");
            if (leftoverRefund.IsFailure)
                return Result.Failure(leftoverRefund.Error!);
            contract.FundsHeldEgp = 0;
            _contracts.Update(contract);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    private async Task ReduceFundsHeldAsync(Contract contract, decimal amountEgp)
    {
        amountEgp = decimal.Round(amountEgp, 2, MidpointRounding.AwayFromZero);
        if (amountEgp <= 0)
            return;

        var remaining = (contract.FundsHeldEgp ?? 0m) - amountEgp;
        if (remaining < 0)
            remaining = 0;
        contract.FundsHeldEgp = remaining;
        _contracts.Update(contract);
        await Task.CompletedTask;
    }

    private async Task<bool> CanReleaseAsync(Guid contractId, Transaction milestone)
    {
        var isDeposit = milestone.Sequence == 1
            || (milestone.PaymentMethod?.Contains("Deposit", StringComparison.OrdinalIgnoreCase) ?? false)
            || (milestone.PaymentMethod?.Contains("Advance", StringComparison.OrdinalIgnoreCase) ?? false);

        if (isDeposit)
            return true;

        var fulfillment = await _fulfillments.GetByContractIdAsync(contractId);
        if (fulfillment is null)
            return false;

        return fulfillment.Status is FulfillmentStatus.Received
            or FulfillmentStatus.QualityChecked
            or FulfillmentStatus.Fulfilled;
    }

    private async Task<Result<Contract>> EnsureFactoryContractAsync(Guid userId, Guid contractId)
    {
        var factory = await _factories.GetByUserIdAsync(userId);
        if (factory is null)
        {
            if (await _farms.GetByUserIdAsync(userId) is not null)
                return Result<Contract>.Failure(MockEscrowErrors.Forbidden);
            return Result<Contract>.Failure(FactoryErrors.FactoryNotFound);
        }

        var contract = await _factories.GetContractForFactoryAsync(factory.FactoryId, contractId);
        if (contract is null)
            return Result<Contract>.Failure(PaymentMilestoneErrors.ContractNotFound);

        return Result<Contract>.Success(contract);
    }

    private async Task<Result<Contract>> EnsureFarmContractAsync(Guid userId, Guid contractId)
    {
        var farm = await _farms.GetByUserIdAsync(userId);
        if (farm is null)
        {
            if (await _factories.GetByUserIdAsync(userId) is not null)
                return Result<Contract>.Failure(MockEscrowErrors.Forbidden);
            return Result<Contract>.Failure(FarmErrors.FarmNotFound);
        }

        var contract = await _farms.GetContractForFarmAsync(userId, contractId);
        if (contract is null)
            return Result<Contract>.Failure(PaymentMilestoneErrors.ContractNotFound);

        return Result<Contract>.Success(contract);
    }

    private async Task NotifyAsync(
        Contract contract,
        Guid actorUserId,
        bool actorIsFarm,
        string title,
        string type,
        string message)
    {
        var farmUserId = contract.FarmMatch?.Farm?.UserId;
        var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
        Guid? target = actorIsFarm ? factoryUserId : farmUserId;
        if (target is null || target == Guid.Empty || target == actorUserId)
            return;

        await _notifications.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = target.Value,
            Title = title,
            Message = message,
            Type = type,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    private MockEscrowSessionDto MapSession(EscrowTransaction e, string label) =>
        new()
        {
            EscrowTransactionId = e.EscrowTransactionId,
            ContractId = e.ContractId,
            TransactionId = e.TransactionId,
            MilestoneLabel = label,
            MilestoneAmountEgp = e.MilestoneAmountEgp,
            PlatformFeePercent = e.PlatformFeePercent,
            PlatformFeeEgp = e.PlatformFeeEgp,
            TotalChargedEgp = e.TotalChargedEgp,
            FarmNetEgp = e.FarmNetEgp,
            Currency = e.Currency,
            Status = e.Status.ToString(),
            Gateway = e.Gateway,
            Disclaimer = ActiveDisclaimer
        };

    private static EscrowTransactionDto MapEscrow(EscrowTransaction e) =>
        new()
        {
            EscrowTransactionId = e.EscrowTransactionId,
            ContractId = e.ContractId,
            TransactionId = e.TransactionId,
            MilestoneAmountEgp = e.MilestoneAmountEgp,
            PlatformFeePercent = e.PlatformFeePercent,
            PlatformFeeEgp = e.PlatformFeeEgp,
            TotalChargedEgp = e.TotalChargedEgp,
            FarmNetEgp = e.FarmNetEgp,
            Currency = e.Currency,
            Status = e.Status.ToString(),
            Gateway = e.Gateway,
            HeldAt = e.HeldAt,
            ReleasedAt = e.ReleasedAt,
            RefundedAt = e.RefundedAt,
            ReleaseReason = e.ReleaseReason,
            RefundReason = e.RefundReason,
            CreatedAt = e.CreatedAt
        };
}
