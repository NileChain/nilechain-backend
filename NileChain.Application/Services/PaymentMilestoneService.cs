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
    private readonly IDisputeRepository _disputes;
    private readonly IFarmRepository _farms;
    private readonly IFactoryRepository _factories;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<Notification> _notifications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly PaymentMilestoneOptions _options;
    private readonly ILogger<PaymentMilestoneService> _logger;

    public PaymentMilestoneService(
        IPaymentMilestoneRepository milestones,
        IDisputeRepository disputes,
        IFarmRepository farms,
        IFactoryRepository factories,
        IRepository<Contract> contracts,
        IRepository<Notification> notifications,
        IUnitOfWork unitOfWork,
        IOptions<PaymentMilestoneOptions> options,
        ILogger<PaymentMilestoneService> logger)
    {
        _milestones = milestones;
        _disputes = disputes;
        _farms = farms;
        _factories = factories;
        _contracts = contracts;
        _notifications = notifications;
        _unitOfWork = unitOfWork;
        _options = options.Value;
        _logger = logger;
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
        if (all.Count == 0)
        {
            return Result<PaymentMilestoneScheduleDto>.Success(new PaymentMilestoneScheduleDto
            {
                ContractId = contractId,
                ContractTotal = totalOk ? total : null,
                ContractTotalUnavailable = !totalOk,
                ContractTotalUnavailableReason = totalOk ? null : reason,
                Disclaimer = StatusTrackingDisclaimer,
                Milestones = []
            });
        }

        var generation = all.Max(t => t.ScheduleGeneration);
        var rows = all.Where(t => t.ScheduleGeneration == generation)
            .OrderBy(t => t.Sequence)
            .ToList();

        return Result<PaymentMilestoneScheduleDto>.Success(MapSchedule(
            contractId,
            totalOk ? total : null,
            totalOk ? null : reason,
            rows));
    }

    public Task<Result<PaymentMilestoneScheduleDto>> MarkPaidAsync(
        Guid factoryUserId,
        Guid contractId,
        Guid transactionId) =>
        TransitionAsync(
            factoryUserId,
            contractId,
            transactionId,
            asFarm: false,
            TransactionStatus.MarkedPaid);

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
        if (await _milestones.HasActiveScheduleAsync(contractId))
            return;

        var contract = await _contracts.GetByIdAsync(contractId);
        if (contract is null
            || contract.Status != ContractStatus.Signed
            || !contract.IsFullySigned)
            return;

        var supply = supplyRequest ?? contract.FarmMatch?.SupplyRequest;
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
            _logger.LogWarning(
                "Payment milestone schedule skipped for ContractId={ContractId}: empty or invalid PaymentMilestones:Schedule config.",
                contractId);
            return;
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
                Note = "Payment milestone schedule opened after full signature (status tracking only)",
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
            return;

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
    }

    private async Task<Result<PaymentMilestoneScheduleDto>> TransitionAsync(
        Guid userId,
        Guid contractId,
        Guid transactionId,
        bool asFarm,
        TransactionStatus to)
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

        await using var dbTx = await _unitOfWork.BeginTransactionAsync();

        var ok = await _milestones.TryAtomicTransitionAsync(
            milestone.TransactionId,
            from,
            to,
            DateTime.UtcNow,
            requireNoActiveDispute: true);

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
                ? "Factory marked milestone paid (status tracking only)"
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

    private static PaymentMilestoneScheduleDto MapSchedule(
        Guid contractId,
        decimal? contractTotal,
        string? unavailableReason,
        IReadOnlyList<Transaction> rows) =>
        new()
        {
            ContractId = contractId,
            ContractTotal = contractTotal,
            ContractTotalUnavailable = contractTotal is null,
            ContractTotalUnavailableReason = unavailableReason,
            Disclaimer = StatusTrackingDisclaimer,
            ScheduleGeneration = rows.Count > 0 ? rows[0].ScheduleGeneration : null,
            IsVoided = rows.Count > 0 && rows.All(r => r.Status == TransactionStatus.Voided),
            Milestones = rows.Select(MapMilestone).ToList()
        };

    private static PaymentMilestoneDto MapMilestone(Transaction t) =>
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
            VoidedAt = t.VoidedAt,
            CreatedAt = t.CreatedAt,
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
}
