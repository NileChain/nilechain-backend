using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Payment;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Options;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public sealed class PaymentMilestoneService : IPaymentMilestoneService
{
    public const string StatusTrackingDisclaimer =
        "Status tracking only — not a payment gateway.";

    private readonly IPaymentMilestoneRepository _milestones;
    private readonly IEscrowTransactionRepository _escrows;
    private readonly IDisputeRepository _disputes;
    private readonly IFarmRepository _farms;
    private readonly IFactoryRepository _factories;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaymentMilestoneOptions _options;
    private readonly MockPaymentOptions _paymentOptions;
    private readonly ILogger<PaymentMilestoneService> _logger;
    private readonly ICloudinaryService _cloudinary;

    public PaymentMilestoneService(
        IPaymentMilestoneRepository milestones,
        IEscrowTransactionRepository escrows,
        IDisputeRepository disputes,
        IFarmRepository farms,
        IFactoryRepository factories,
        IRepository<Contract> contracts,
        IRepository<Notification> notifications,
        IUnitOfWork unitOfWork,
        IOptions<PaymentMilestoneOptions> options,
        IOptions<MockPaymentOptions> paymentOptions,
        ILogger<PaymentMilestoneService> logger,
        ICloudinaryService cloudinary)
    {
        _milestones = milestones;
        _escrows = escrows;
        _disputes = disputes;
        _farms = farms;
        _factories = factories;
        _contracts = contracts;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _paymentOptions = paymentOptions.Value;
        _logger = logger;
        _cloudinary = cloudinary;
    }

    public async Task<Result<PaymentMilestoneScheduleDto>> GetByContractAsync(
        Guid userId,
        Guid contractId,
        bool asFarm)
    {
        var access = await EnsurePartyAccessAsync(userId, contractId, asFarm);
        if (access.IsFailure)
            return Result<PaymentMilestoneScheduleDto>.Failure(access.Error!);

        var contract = access.Value;
        var supply = contract.FarmMatch?.SupplyRequest;
        var totalOk = ContractCommercialTotal.TryCompute(supply, out var total, out var reason);

        var all = await _milestones.GetByContractIdAsync(contractId);
        var farm = contract.FarmMatch?.Farm;
        var frozen = await _disputes.HasActiveDisputeAsync(contractId);
        // Factory-only: off-platform transfer hint on signed-contract schedules.
        var payout = asFarm ? null : MapPayout(farm);

        // Backfill: older signed contracts (or failed create-on-sign) open schedule on first read.
        if (all.Count == 0
            && contract.Status == ContractStatus.Signed
            && contract.IsFullySigned)
        {
            await EnsureCreatedForSignedContractAsync(contractId, userId, supply);
            all = await _milestones.GetByContractIdAsync(contractId);
            totalOk = ContractCommercialTotal.TryCompute(supply, out total, out reason);
        }

        if (all.Count == 0)
        {
            return Result<PaymentMilestoneScheduleDto>.Success(new PaymentMilestoneScheduleDto
            {
                ContractId = contractId,
                ContractTotal = totalOk ? total : null,
                ContractTotalUnavailable = !totalOk,
                ContractTotalUnavailableReason = totalOk ? null : reason,
                Disclaimer = ResolveDisclaimer(),
                MockGatewayEnabled = _paymentOptions.MockGatewayEnabled,
                WalletEnabled = _paymentOptions.WalletEnabled,
                PlatformFeePercent = ResolveFeePercent(),
                FarmPayoutDetails = payout,
                PaymentsFrozenByDispute = frozen,
                Milestones = [],
                Escrows = []
            });
        }

        var generation = all.Max(t => t.ScheduleGeneration);
        var rows = all.Where(t => t.ScheduleGeneration == generation)
            .OrderBy(t => t.Sequence)
            .ToList();

        var escrows = await _escrows.GetByContractIdAsync(contractId);

        return Result<PaymentMilestoneScheduleDto>.Success(MapSchedule(
            contractId,
            totalOk ? total : null,
            totalOk ? null : reason,
            rows,
            payout,
            frozen,
            escrows));
    }

    public Task<Result<PaymentMilestoneScheduleDto>> MarkPaidAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid transactionId,
        Microsoft.AspNetCore.Http.IFormFile? receipt = null)
    {
        if (_paymentOptions.MockGatewayEnabled)
            return Task.FromResult(Result<PaymentMilestoneScheduleDto>.Failure(MockEscrowErrors.UseMockPay));

        return TransitionAsync(
            factoryUserId,
            contractId,
            transactionId,
            asFarm: false,
            TransactionStatus.MarkedPaid,
            receipt);
    }

    public Task<Result<PaymentMilestoneScheduleDto>> ConfirmReceivedAsync(
        Guid farmUserId,
        Guid contractId,
        Guid transactionId) =>
        TransitionAsync(
            farmUserId,
            contractId,
            transactionId,
            asFarm: true,
            TransactionStatus.Completed);

    public async Task EnsureCreatedForSignedContractAsync(
        Guid contractId,
        Guid actorUserId,
        SupplyRequest? supplyRequest)
    {
        try
        {
            await EnsureCreatedCoreAsync(contractId, actorUserId, supplyRequest);
        }
        catch (Exception ex)
        {
            // Never fail the signing response because milestone creation failed —
            // GetByContractAsync will backfill on next open.
            _logger.LogError(
                ex,
                "Payment milestone EnsureCreated failed for ContractId={ContractId}",
                contractId);
        }
    }

    private async Task EnsureCreatedCoreAsync(
        Guid contractId,
        Guid actorUserId,
        SupplyRequest? supplyRequest)
    {
        if (await _milestones.HasActiveScheduleAsync(contractId))
            return;

        // Prefer caller-supplied supply; reload contract flags from the tracked/DB entity.
        var contract = await _contracts.GetByIdAsync(contractId);
        if (contract is null)
            return;

        // After SaveChanges in approve, Status/signatures must already be Signed.
        // If a stale tracked instance is missing timestamps, still allow create when
        // the caller passed a supply request (signing path) OR DB says fully signed.
        var looksSigned = contract.Status == ContractStatus.Signed && contract.IsFullySigned;
        if (!looksSigned && supplyRequest is null)
            return;
        if (!looksSigned && supplyRequest is not null)
        {
            // Signing path: trust the caller after full signature; force status check soft.
            if (!contract.IsFullySigned && contract.FactorySignedAt is null && contract.FarmSignedAt is null)
                return;
        }

        var supply = supplyRequest ?? contract.FarmMatch?.SupplyRequest;
        if (supply is null)
        {
            // Reload supply via farms/factories not available here — try match nav if tracked.
            _logger.LogWarning(
                "Payment milestone schedule skipped for ContractId={ContractId}: supply request missing.",
                contractId);
            return;
        }

        if (!ContractCommercialTotal.TryCompute(supply, out var contractTotal, out var reason))
        {
            _logger.LogWarning(
                "Payment milestone schedule skipped for ContractId={ContractId}: {Reason}",
                contractId,
                reason);
            return;
        }

        var steps = NormalizeSchedule(_options.Schedule);
        if (steps.Count == 0)
        {
            // Config bind may wipe defaults — fall back to 30/70.
            steps =
            [
                new PaymentMilestoneStepOptions
                {
                    Key = "Deposit",
                    Label = "Deposit (advance)",
                    Percent = 30
                },
                new PaymentMilestoneStepOptions
                {
                    Key = "OnDelivery",
                    Label = "On delivery",
                    Percent = 70
                }
            ];
        }

        var generation = await _milestones.GetMaxScheduleGenerationAsync(contractId) + 1;
        var now = DateTime.UtcNow;
        var rows = new List<Transaction>();
        var events = new List<TransactionEvent>();

        for (var i = 0; i < steps.Count; i++)
        {
            var step = steps[i];
            var amount = decimal.Round(
                contractTotal * step.Percent / 100m,
                2,
                MidpointRounding.AwayFromZero);

            var dueDate = ResolveMilestoneDueDate(step.Key, i, steps.Count, now, supply.DeliveryDate);
            var txRow = new Transaction
            {
                TransactionId = Guid.NewGuid(),
                ContractId = contractId,
                ScheduleGeneration = generation,
                Sequence = i + 1,
                Label = step.Label,
                PaymentMethod = step.Key,
                Percent = step.Percent,
                Amount = amount,
                Status = TransactionStatus.Pending,
                DueDate = dueDate,
                CreatedAt = now
            };
            rows.Add(txRow);
            events.Add(new TransactionEvent
            {
                EventId = Guid.NewGuid(),
                TransactionId = txRow.TransactionId,
                FromStatus = null,
                ToStatus = TransactionStatus.Pending,
                ActorUserId = actorUserId,
                Note = "Payment milestone schedule opened after full signature",
                CreatedAt = now
            });
        }

        // Fix rounding drift on the last milestone so amounts sum to contractTotal.
        var drift = contractTotal - rows.Sum(r => r.Amount);
        if (drift != 0 && rows.Count > 0)
            rows[^1].Amount += drift;

        try
        {
            await _milestones.AddRangeAsync(rows);
            await _milestones.AddEventsAsync(events);
            await _unitOfWork.SaveChangesAsync();
            _logger.LogInformation(
                "Created {Count} payment milestones for ContractId={ContractId} total={Total}",
                rows.Count,
                contractId,
                contractTotal);
        }
        catch (DbUpdateException ex) when (UniqueConstraintViolation.IsViolation(ex))
        {
            // Concurrent second-signer also created the schedule — treat as success.
        }
    }

    public async Task VoidForContractAsync(Guid contractId, Guid actorUserId, string reason)
    {
        var all = await _milestones.GetByContractIdAsync(contractId, includeEvents: false);
        var active = all.Where(t => PaymentMilestoneTransitions.CanVoid(t.Status)).ToList();
        if (active.Count == 0)
        {
            await FailActiveEscrowsForContractAsync(contractId, actorUserId, reason);
            return;
        }

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;

        foreach (var milestone in active)
        {
            var from = milestone.Status;
            var ok = await _milestones.TryAtomicTransitionAsync(
                milestone.TransactionId,
                from,
                TransactionStatus.Voided,
                now);
            if (!ok)
                continue;

            await _milestones.AddEventAsync(new TransactionEvent
            {
                EventId = Guid.NewGuid(),
                TransactionId = milestone.TransactionId,
                FromStatus = from,
                ToStatus = TransactionStatus.Voided,
                ActorUserId = actorUserId,
                Note = reason,
                CreatedAt = now
            });
        }

        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();

        await FailActiveEscrowsForContractAsync(contractId, actorUserId, reason);
    }

    private async Task FailActiveEscrowsForContractAsync(Guid contractId, Guid actorUserId, string reason)
    {
        var escrows = await _escrows.GetByContractIdAsync(contractId);
        var now = DateTime.UtcNow;
        foreach (var e in escrows.Where(x =>
                     x.Status is EscrowStatus.Created or EscrowStatus.Pending or EscrowStatus.Held))
        {
            await _escrows.TryAtomicStatusAsync(
                e.EscrowTransactionId,
                e.Status,
                EscrowStatus.Failed,
                now,
                $"Voided with milestones: {reason}");
        }

        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Result<PaymentMilestoneScheduleDto>> TransitionAsync(
        Guid userId,
        Guid contractId,
        Guid transactionId,
        bool asFarm,
        TransactionStatus to,
        Microsoft.AspNetCore.Http.IFormFile? receipt = null)
    {
        if (asFarm && !PaymentMilestoneTransitions.IsFarmAction(to))
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.Forbidden);
        if (!asFarm && !PaymentMilestoneTransitions.IsFactoryAction(to))
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.Forbidden);

        var access = await EnsurePartyAccessAsync(userId, contractId, asFarm);
        if (access.IsFailure)
            return Result<PaymentMilestoneScheduleDto>.Failure(access.Error!);

        var contract = access.Value;
        if (contract.Status != ContractStatus.Signed || !contract.IsFullySigned)
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.ContractNotSigned);

        var milestone = await _milestones.GetByIdAsync(transactionId, includeEvents: false);
        if (milestone is null || milestone.ContractId != contractId)
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.MilestoneNotFound);

        if (milestone.Status == TransactionStatus.Voided)
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.Voided);

        var from = milestone.Status;
        if (!PaymentMilestoneTransitions.CanTransition(from, to))
            return Result<PaymentMilestoneScheduleDto>.Failure(PaymentMilestoneErrors.InvalidTransition);

        string? receiptUrl = null;
        string? receiptPublicId = null;
        string? receiptFileName = null;
        if (receipt is not null && to == TransactionStatus.MarkedPaid)
        {
            await using var probe = receipt.OpenReadStream();
            var validation = Validation.FileUploadValidation.Validate(
                receipt.FileName,
                receipt.ContentType,
                receipt.Length,
                probe);
            if (!validation.IsValid)
            {
                return Result<PaymentMilestoneScheduleDto>.Failure(new Error(
                    validation.ErrorCode ?? "File.Invalid",
                    validation.ErrorMessage ?? "Invalid receipt file."));
            }

            var uploaded = await _cloudinary.UploadAsync(receipt);
            receiptUrl = uploaded.Url;
            receiptPublicId = uploaded.PublicId;
            receiptFileName = Path.GetFileName(receipt.FileName);
        }

        await using var dbTx = await _unitOfWork.BeginTransactionAsync();

        var ok = await _milestones.TryAtomicTransitionAsync(
            milestone.TransactionId,
            from,
            to,
            DateTime.UtcNow,
            requireNoActiveDispute: true,
            receiptUrl,
            receiptPublicId,
            receiptFileName);

        if (!ok)
        {
            var frozen = await _disputes.HasActiveDisputeAsync(contractId);
            return Result<PaymentMilestoneScheduleDto>.Failure(
                frozen ? PaymentMilestoneErrors.FrozenByDispute : PaymentMilestoneErrors.Conflict);
        }

        await _milestones.AddEventAsync(new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            TransactionId = milestone.TransactionId,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = userId,
            Note = to == TransactionStatus.MarkedPaid
                ? (receiptUrl is null
                    ? "Factory marked milestone paid (status tracking only)"
                    : "Factory marked milestone paid with receipt (status tracking only)")
                : "Farm confirmed payment received (status tracking only)",
            CreatedAt = DateTime.UtcNow
        });

        await NotifyCounterpartyAsync(contract, userId, asFarm, to, milestone.Label);
        await _unitOfWork.SaveChangesAsync();
        await dbTx.CommitAsync();

        return await GetByContractAsync(userId, contractId, asFarm);
    }

    private async Task NotifyCounterpartyAsync(
        Contract contract,
        Guid actorUserId,
        bool actorIsFarm,
        TransactionStatus to,
        string milestoneLabel)
    {
        var farmUserId = contract.FarmMatch?.Farm?.UserId;
        var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
        Guid? target = actorIsFarm ? factoryUserId : farmUserId;
        if (target is null || target == Guid.Empty || target == actorUserId)
            return;

        var (title, type, message) = to switch
        {
            TransactionStatus.MarkedPaid => (
                "Payment status updated",
                "PaymentMarked",
                $"Factory marked '{milestoneLabel}' as paid (status tracking only — not a payment gateway)."),
            TransactionStatus.Completed => (
                "Payment receipt confirmed",
                "PaymentReceived",
                $"Farm confirmed receipt for '{milestoneLabel}' (status tracking only — not a payment gateway)."),
            _ => (
                "Payment status updated",
                "PaymentStatus",
                $"Payment milestone '{milestoneLabel}' is now {to} (status tracking only).")
        };

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

    private async Task<Result<Contract>> EnsurePartyAccessAsync(Guid userId, Guid contractId, bool asFarm)
    {
        if (asFarm)
        {
            var farm = await _farms.GetByUserIdAsync(userId);
            if (farm is null)
            {
                // Factory account calling a farm-only action.
                if (await _factories.GetByUserIdAsync(userId) is not null)
                    return Result<Contract>.Failure(PaymentMilestoneErrors.Forbidden);
                return Result<Contract>.Failure(FarmErrors.FarmNotFound);
            }

            var contract = await _farms.GetContractForFarmAsync(userId, contractId);
            if (contract is null)
                return Result<Contract>.Failure(PaymentMilestoneErrors.ContractNotFound);

            return Result<Contract>.Success(contract);
        }

        var factory = await _factories.GetByUserIdAsync(userId);
        if (factory is null)
        {
            // Farm account calling a factory-only action.
            if (await _farms.GetByUserIdAsync(userId) is not null)
                return Result<Contract>.Failure(PaymentMilestoneErrors.Forbidden);
            return Result<Contract>.Failure(FactoryErrors.FactoryNotFound);
        }

        var factoryContract = await _factories.GetContractForFactoryAsync(factory.FactoryId, contractId);
        if (factoryContract is null)
            return Result<Contract>.Failure(PaymentMilestoneErrors.ContractNotFound);

        return Result<Contract>.Success(factoryContract);
    }

    private static List<PaymentMilestoneStepOptions> NormalizeSchedule(
        IEnumerable<PaymentMilestoneStepOptions>? configured)
    {
        var steps = (configured ?? [])
            .Where(s => !string.IsNullOrWhiteSpace(s.Key)
                        && !string.IsNullOrWhiteSpace(s.Label)
                        && s.Percent > 0)
            .ToList();

        var sum = steps.Sum(s => s.Percent);
        if (steps.Count == 0 || sum <= 0)
            return [];

        // Allow slight float drift; reject obviously broken configs.
        if (sum < 99.9m || sum > 100.1m)
            return [];

        return steps;
    }

    private static FarmPayoutDetailsDto? MapPayout(Farm? farm)
    {
        if (farm is null)
            return null;
        if (string.IsNullOrWhiteSpace(farm.BankName)
            && string.IsNullOrWhiteSpace(farm.AccountHolderName)
            && string.IsNullOrWhiteSpace(farm.BankAccountNumber)
            && string.IsNullOrWhiteSpace(farm.Iban))
            return null;

        return new FarmPayoutDetailsDto
        {
            BankName = farm.BankName,
            AccountHolderName = farm.AccountHolderName,
            AccountMasked = MaskAccount(farm.BankAccountNumber),
            Iban = farm.Iban
        };
    }

    private static string? MaskAccount(string? account)
    {
        if (string.IsNullOrWhiteSpace(account))
            return null;
        var digits = account.Trim();
        if (digits.Length <= 4)
            return new string('*', digits.Length);
        return new string('*', Math.Min(4, digits.Length - 4)) + digits[^4..];
    }

    private string ResolveDisclaimer() =>
        !_paymentOptions.MockGatewayEnabled
            ? StatusTrackingDisclaimer
            : _paymentOptions.WalletEnabled
                ? MockEscrowPaymentService.WalletDisclaimer
                : MockEscrowPaymentService.MockDisclaimer;

    private decimal ResolveFeePercent()
    {
        var p = _paymentOptions.PlatformFeePercent;
        if (p < 0) return 0;
        return Math.Min(p, 30m);
    }

    private PaymentMilestoneScheduleDto MapSchedule(
        Guid contractId,
        decimal? contractTotal,
        string? unavailableReason,
        IReadOnlyList<Transaction> rows,
        FarmPayoutDetailsDto? payout,
        bool paymentsFrozen,
        IReadOnlyList<EscrowTransaction> escrows)
    {
        var latestByTx = escrows
            .GroupBy(e => e.TransactionId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.CreatedAt).First());

        return new PaymentMilestoneScheduleDto
        {
            ContractId = contractId,
            ContractTotal = contractTotal,
            ContractTotalUnavailable = contractTotal is null,
            ContractTotalUnavailableReason = unavailableReason,
            Disclaimer = ResolveDisclaimer(),
            MockGatewayEnabled = _paymentOptions.MockGatewayEnabled,
            WalletEnabled = _paymentOptions.WalletEnabled,
            PlatformFeePercent = ResolveFeePercent(),
            FarmPayoutDetails = payout,
            ScheduleGeneration = rows.Count > 0 ? rows[0].ScheduleGeneration : null,
            IsVoided = rows.Count > 0 && rows.All(r => r.Status == TransactionStatus.Voided),
            PaymentsFrozenByDispute = paymentsFrozen,
            Milestones = rows.Select(t => MapMilestone(t, latestByTx.GetValueOrDefault(t.TransactionId))).ToList(),
            Escrows = escrows.Select(e => new EscrowTransactionDto
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
            }).ToList()
        };
    }

    private static PaymentMilestoneDto MapMilestone(Transaction t, EscrowTransaction? escrow) =>
        new()
        {
            TransactionId = t.TransactionId,
            Sequence = t.Sequence,
            Key = t.PaymentMethod ?? string.Empty,
            Label = t.Label,
            Percent = t.Percent,
            Amount = t.Amount,
            Status = t.Status.ToString(),
            PaidAt = t.PaidAt,
            ReceivedAt = t.ReceivedAt,
            DueDate = t.DueDate,
            IsOverdue = t.DueDate is not null
                && t.DueDate.Value.Date < DateTime.UtcNow.Date
                && t.Status is TransactionStatus.Pending
                    or TransactionStatus.MarkedPaid
                    or TransactionStatus.EscrowHeld,
            VoidedAt = t.VoidedAt,
            CreatedAt = t.CreatedAt,
            ReceiptUrl = t.ReceiptUrl,
            ReceiptFileName = t.ReceiptFileName,
            ReceiptUploadedAt = t.ReceiptUploadedAt,
            ActiveEscrowTransactionId = escrow is not null
                && escrow.Status is EscrowStatus.Created or EscrowStatus.Pending or EscrowStatus.Held
                    ? escrow.EscrowTransactionId
                    : escrow?.EscrowTransactionId,
            EscrowStatus = escrow?.Status.ToString(),
            PlatformFeeEgp = escrow?.PlatformFeeEgp,
            TotalChargedEgp = escrow?.TotalChargedEgp,
            FarmNetEgp = escrow?.FarmNetEgp,
            Events = (t.Events ?? Array.Empty<TransactionEvent>())
                .OrderBy(e => e.CreatedAt)
                .Select(e => new PaymentMilestoneEventDto
                {
                    EventId = e.EventId,
                    FromStatus = e.FromStatus?.ToString(),
                    ToStatus = e.ToStatus.ToString(),
                    ActorUserId = e.ActorUserId,
                    Note = e.Note,
                    CreatedAt = e.CreatedAt
                })
                .ToList()
        };

    private static DateTime ResolveMilestoneDueDate(
        string key,
        int index,
        int totalSteps,
        DateTime now,
        DateTime? deliveryDate)
    {
        if (key.Contains("Deposit", StringComparison.OrdinalIgnoreCase)
            || key.Contains("Advance", StringComparison.OrdinalIgnoreCase)
            || index == 0)
        {
            return now.Date.AddDays(7);
        }

        if (deliveryDate is not null)
            return deliveryDate.Value.Date;

        if (totalSteps <= 1)
            return now.Date.AddDays(30);

        var spanDays = 7 + ((index * 23.0) / Math.Max(1, totalSteps - 1));
        return now.Date.AddDays((int)Math.Round(spanDays));
    }
}
