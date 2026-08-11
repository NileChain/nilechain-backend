using Microsoft.EntityFrameworkCore;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Fulfillment;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public sealed class FulfillmentService : IFulfillmentService
{
    private readonly IFulfillmentRepository _fulfillments;
    private readonly IDisputeRepository _disputes;
    private readonly IFarmRepository _farms;
    private readonly IFactoryRepository _factories;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<Notification> _notifications;
    private readonly IRepository<SupplyRequest> _supplyRequests;
    private readonly IUnitOfWork _unitOfWork;

    public FulfillmentService(
        IFulfillmentRepository fulfillments,
        IDisputeRepository disputes,
        IFarmRepository farms,
        IFactoryRepository factories,
        IRepository<Contract> contracts,
        IRepository<Notification> notifications,
        IRepository<SupplyRequest> supplyRequests,
        IUnitOfWork unitOfWork)
    {
        _fulfillments = fulfillments;
        _disputes = disputes;
        _farms = farms;
        _factories = factories;
        _contracts = contracts;
        _notifications = notifications;
        _supplyRequests = supplyRequests;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FulfillmentDto>> GetByContractAsync(Guid userId, Guid contractId, bool asFarm)
    {
        var access = await EnsurePartyAccessAsync(userId, contractId, asFarm);
        if (access.IsFailure)
            return Result<FulfillmentDto>.Failure(access.Error!);

        var fulfillment = await _fulfillments.GetByContractIdAsync(contractId);
        if (fulfillment is null)
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.NotFound);

        return Result<FulfillmentDto>.Success(Map(fulfillment));
    }

    public Task<Result<FulfillmentDto>> MarkShippedAsync(Guid farmUserId, Guid contractId) =>
        TransitionAsync(farmUserId, contractId, asFarm: true, FulfillmentStatus.Shipped);

    public Task<Result<FulfillmentDto>> MarkReceivedAsync(Guid factoryUserId, Guid contractId) =>
        TransitionAsync(factoryUserId, contractId, asFarm: false, FulfillmentStatus.Received);

    public Task<Result<FulfillmentDto>> MarkQualityCheckedAsync(
        Guid factoryUserId,
        Guid contractId,
        string? notes) =>
        TransitionAsync(factoryUserId, contractId, asFarm: false, FulfillmentStatus.QualityChecked, notes);

    public Task<Result<FulfillmentDto>> MarkFulfilledAsync(Guid factoryUserId, Guid contractId) =>
        TransitionAsync(factoryUserId, contractId, asFarm: false, FulfillmentStatus.Fulfilled);

    public async Task EnsureCreatedForSignedContractAsync(
        Guid contractId,
        Guid actorUserId,
        DateTime? plannedShipDate = null)
    {
        var existing = await _fulfillments.GetByContractIdAsync(contractId, includeEvents: false);
        if (existing is not null)
            return;

        var contract = await _contracts.GetByIdAsync(contractId);
        if (contract is null
            || contract.Status != ContractStatus.Signed
            || !contract.IsFullySigned)
            return;

        var fulfillment = new Fulfillment
        {
            FulfillmentId = Guid.NewGuid(),
            ContractId = contractId,
            Status = FulfillmentStatus.Planned,
            PlannedShipDate = plannedShipDate,
            CreatedAt = DateTime.UtcNow
        };

        var createdEvent = new FulfillmentEvent
        {
            EventId = Guid.NewGuid(),
            FulfillmentId = fulfillment.FulfillmentId,
            FromStatus = null,
            ToStatus = FulfillmentStatus.Planned,
            ActorUserId = actorUserId,
            Note = "Fulfillment opened after full signature",
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await _fulfillments.AddAsync(fulfillment);
            await _fulfillments.AddEventAsync(createdEvent);
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (UniqueConstraintViolation.IsViolation(ex))
        {
            // Concurrent second-signer also created fulfillment — treat as success.
        }
    }

    public async Task VoidForContractAsync(Guid contractId, Guid actorUserId, string reason)
    {
        var fulfillment = await _fulfillments.GetByContractIdAsync(contractId, includeEvents: false);
        if (fulfillment is null || FulfillmentTransitions.IsTerminal(fulfillment.Status))
            return;

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        var from = fulfillment.Status;
        var ok = await _fulfillments.TryAtomicTransitionAsync(
            fulfillment.FulfillmentId,
            from,
            FulfillmentStatus.Voided,
            DateTime.UtcNow);

        if (!ok)
            return;

        await _fulfillments.AddEventAsync(new FulfillmentEvent
        {
            EventId = Guid.NewGuid(),
            FulfillmentId = fulfillment.FulfillmentId,
            FromStatus = from,
            ToStatus = FulfillmentStatus.Voided,
            ActorUserId = actorUserId,
            Note = reason,
            CreatedAt = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public async Task<Result<StuckFulfillmentListDto>> GetStuckDeliveriesAsync(int page, int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var asOf = DeliveryDatePolicy.ToUtcStorage(DateTime.UtcNow.Date);

        var total = await _fulfillments.CountStuckPlannedAsync(asOf);
        var items = await _fulfillments.GetStuckPlannedAsync(asOf, (page - 1) * pageSize, pageSize);

        return Result<StuckFulfillmentListDto>.Success(new StuckFulfillmentListDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(f => new StuckFulfillmentDto
            {
                FulfillmentId = f.FulfillmentId,
                ContractId = f.ContractId,
                Status = f.Status.ToString(),
                PlannedShipDate = f.PlannedShipDate,
                CreatedAt = f.CreatedAt,
                FarmName = f.Contract.FarmMatch?.Farm?.Name,
                FactoryName = f.Contract.FarmMatch?.SupplyRequest?.Factory?.Name
            }).ToList()
        });
    }

    private async Task<Result<FulfillmentDto>> TransitionAsync(
        Guid userId,
        Guid contractId,
        bool asFarm,
        FulfillmentStatus to,
        string? qualityNotes = null)
    {
        if (asFarm && !FulfillmentTransitions.IsFarmAction(to))
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.Forbidden);
        if (!asFarm && !FulfillmentTransitions.IsFactoryAction(to))
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.Forbidden);

        var access = await EnsurePartyAccessAsync(userId, contractId, asFarm);
        if (access.IsFailure)
            return Result<FulfillmentDto>.Failure(access.Error!);

        var contract = access.Value;
        if (contract.Status != ContractStatus.Signed || !contract.IsFullySigned)
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.ContractNotSigned);

        var fulfillment = await _fulfillments.GetByContractIdAsync(contractId);
        if (fulfillment is null)
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.NotFound);

        if (fulfillment.Status == FulfillmentStatus.Voided)
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.Voided);

        var from = fulfillment.Status;
        if (!FulfillmentTransitions.CanTransition(from, to))
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.InvalidTransition);

        await using var tx = await _unitOfWork.BeginTransactionAsync();

        var ok = await _fulfillments.TryAtomicTransitionAsync(
            fulfillment.FulfillmentId,
            from,
            to,
            DateTime.UtcNow,
            qualityNotes,
            requireNoActiveDispute: true);

        if (!ok)
        {
            // Classify: active dispute in the same WHERE vs concurrent status change.
            var frozen = await _disputes.HasActiveDisputeAsync(contractId);
            return Result<FulfillmentDto>.Failure(
                frozen ? FulfillmentErrors.FrozenByDispute : FulfillmentErrors.Conflict);
        }

        await _fulfillments.AddEventAsync(new FulfillmentEvent
        {
            EventId = Guid.NewGuid(),
            FulfillmentId = fulfillment.FulfillmentId,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = userId,
            Note = qualityNotes,
            CreatedAt = DateTime.UtcNow
        });

        if (to == FulfillmentStatus.Fulfilled)
        {
            var request = contract.FarmMatch?.SupplyRequest;
            if (request is not null)
            {
                request.Status = SupplyRequestStatus.Fulfilled;
                _supplyRequests.Update(request);
            }
        }

        await NotifyCounterpartyAsync(contract, userId, asFarm, to);
        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();

        var updated = await _fulfillments.GetByContractIdAsync(contractId);
        return Result<FulfillmentDto>.Success(Map(updated!));
    }

    private async Task NotifyCounterpartyAsync(
        Contract contract,
        Guid actorUserId,
        bool actorIsFarm,
        FulfillmentStatus to)
    {
        var farmUserId = contract.FarmMatch?.Farm?.UserId;
        var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
        Guid? target = actorIsFarm ? factoryUserId : farmUserId;
        if (target is null || target == Guid.Empty || target == actorUserId)
            return;

        var (title, type) = to switch
        {
            FulfillmentStatus.Shipped => ("Shipment marked", "FulfillmentShipped"),
            FulfillmentStatus.Received => ("Delivery received", "FulfillmentReceived"),
            FulfillmentStatus.QualityChecked => ("Quality check recorded", "FulfillmentQualityChecked"),
            FulfillmentStatus.Fulfilled => ("Contract fulfilled", "FulfillmentFulfilled"),
            _ => ("Fulfillment updated", "Fulfillment")
        };

        await _notifications.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = target.Value,
            Title = title,
            Message = $"Fulfillment status is now {to} for your supply contract.",
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
                return Result<Contract>.Failure(FarmErrors.FarmNotFound);

            var contract = await _farms.GetContractForFarmAsync(userId, contractId);
            if (contract is null)
                return Result<Contract>.Failure(FulfillmentErrors.ContractNotFound);

            return Result<Contract>.Success(contract);
        }

        var factory = await _factories.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<Contract>.Failure(FactoryErrors.FactoryNotFound);

        var factoryContract = await _factories.GetContractForFactoryAsync(factory.FactoryId, contractId);
        if (factoryContract is null)
            return Result<Contract>.Failure(FulfillmentErrors.ContractNotFound);

        return Result<Contract>.Success(factoryContract);
    }

    private static FulfillmentDto Map(Fulfillment f) => new()
    {
        FulfillmentId = f.FulfillmentId,
        ContractId = f.ContractId,
        Status = f.Status.ToString(),
        PlannedShipDate = f.PlannedShipDate,
        ShippedAt = f.ShippedAt,
        ReceivedAt = f.ReceivedAt,
        QualityCheckedAt = f.QualityCheckedAt,
        FulfilledAt = f.FulfilledAt,
        VoidedAt = f.VoidedAt,
        QualityNotes = f.QualityNotes,
        Events = (f.Events ?? Array.Empty<FulfillmentEvent>())
            .OrderBy(e => e.CreatedAt)
            .Select(e => new FulfillmentEventDto
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
