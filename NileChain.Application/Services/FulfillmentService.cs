using Microsoft.EntityFrameworkCore;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Fulfillment;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Notifications;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public sealed class FulfillmentService : IFulfillmentService
{
    private readonly IFulfillmentRepository _fulfillments;
    private readonly IDisputeRepository _disputes;
    private readonly IPaymentMilestoneRepository _milestones;
    private readonly IFarmRepository _farms;
    private readonly IFactoryRepository _factories;
    private readonly IRepository<Contract> _contracts;
    private readonly IRepository<Notification> _notifications;
    private readonly IRepository<SupplyRequest> _supplyRequests;
    private readonly IMockEscrowPaymentService _escrowPayments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IOutboundChannel? _outbound;

    public FulfillmentService(
        IFulfillmentRepository fulfillments,
        IDisputeRepository disputes,
        IPaymentMilestoneRepository milestones,
        IFarmRepository farms,
        IFactoryRepository factories,
        IRepository<Contract> contracts,
        IRepository<Notification> notifications,
        IRepository<SupplyRequest> supplyRequests,
        IMockEscrowPaymentService escrowPayments,
        IUnitOfWork unitOfWork,
        IOutboundChannel? outbound = null)
    {
        _fulfillments = fulfillments;
        _disputes = disputes;
        _milestones = milestones;
        _farms = farms;
        _factories = factories;
        _contracts = contracts;
        _notifications = notifications;
        _supplyRequests = supplyRequests;
        _escrowPayments = escrowPayments;
        _unitOfWork = unitOfWork;
        _outbound = outbound;
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

    public Task<Result<FulfillmentDto>> MarkShippedAsync(
        Guid farmUserId,
        Guid contractId,
        ShipFulfillmentRequest? request = null) =>
        TransitionAsync(
            farmUserId,
            contractId,
            asFarm: true,
            FulfillmentStatus.Shipped,
            carrier: request?.Carrier?.Trim(),
            trackingNumber: request?.TrackingNumber?.Trim(),
            shippedNotes: request?.Notes?.Trim());

    public Task<Result<FulfillmentDto>> MarkReceivedAsync(
        Guid factoryUserId,
        Guid contractId,
        ReceiveFulfillmentRequest? request = null)
    {
        if (request is null || request.WeighedQuantityTons <= 0)
            return Task.FromResult(Result<FulfillmentDto>.Failure(FulfillmentErrors.WeighbridgeRequired));

        var ticket = string.IsNullOrWhiteSpace(request.WeighbridgeTicketUrl)
            ? null
            : request.WeighbridgeTicketUrl.Trim();

        return TransitionAsync(
            factoryUserId,
            contractId,
            asFarm: false,
            FulfillmentStatus.Received,
            weighedQuantityTons: request.WeighedQuantityTons,
            weighbridgeTicketUrl: ticket);
    }

    public Task<Result<FulfillmentDto>> MarkRejectedAtGateAsync(
        Guid factoryUserId,
        Guid contractId,
        RejectAtGateRequest request)
    {
        if (request is null
            || !Enum.TryParse<GateRejectReason>(request.Reason, ignoreCase: true, out var reason))
        {
            return Task.FromResult(Result<FulfillmentDto>.Failure(FulfillmentErrors.InvalidGateReject));
        }

        var notes = request.Notes?.Trim();
        if (reason == GateRejectReason.Other && string.IsNullOrWhiteSpace(notes))
            return Task.FromResult(Result<FulfillmentDto>.Failure(FulfillmentErrors.GateRejectNotesRequired));

        return TransitionAsync(
            factoryUserId,
            contractId,
            asFarm: false,
            FulfillmentStatus.RejectedAtGate,
            requireNoActiveDispute: false,
            gateRejectReason: reason,
            gateRejectNotes: notes);
    }

    public Task<Result<FulfillmentDto>> MarkQualityCheckedAsync(
        Guid factoryUserId,
        Guid contractId,
        QualityCheckRequest? request = null) =>
        TransitionAsync(
            factoryUserId,
            contractId,
            asFarm: false,
            FulfillmentStatus.QualityChecked,
            qualityNotes: request?.Notes?.Trim(),
            acceptedQuantityTons: request?.AcceptedQuantityTons,
            discountPercent: ClampDiscount(request?.DiscountPercent ?? 0m),
            specsMet: request?.SpecsMet,
            specsOutcomeNotes: request?.SpecsOutcomeNotes?.Trim());

    public Task<Result<FulfillmentDto>> MarkFulfilledAsync(Guid factoryUserId, Guid contractId) =>
        TransitionAsync(factoryUserId, contractId, asFarm: false, FulfillmentStatus.Fulfilled);

    public async Task EnsureCreatedForSignedContractAsync(
        Guid contractId,
        Guid actorUserId,
        DateTime? plannedShipDate = null,
        DeliveryPoint? deliveryPoint = null,
        DealParty? freightPayer = null,
        DealParty? transitRisk = null)
    {
        var existing = await _fulfillments.GetByContractIdAsync(contractId, includeEvents: false);
        if (existing is not null)
            return;

        var contract = await _contracts.GetByIdAsync(contractId);
        if (contract is null
            || contract.Status != ContractStatus.Signed
            || !contract.IsFullySigned)
            return;

        var fromRequest = contract.FarmMatch?.SupplyRequest;
        var terms = DeliveryTermsPolicy.Resolve(
            deliveryPoint ?? fromRequest?.DeliveryPoint,
            freightPayer ?? fromRequest?.FreightPayer,
            transitRisk ?? fromRequest?.TransitRisk);

        var fulfillment = new Fulfillment
        {
            FulfillmentId = Guid.NewGuid(),
            ContractId = contractId,
            Status = FulfillmentStatus.Planned,
            PlannedShipDate = plannedShipDate,
            DeliveryPoint = terms.Point,
            FreightPayer = terms.FreightPayer,
            TransitRisk = terms.TransitRisk,
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
        string? qualityNotes = null,
        string? carrier = null,
        string? trackingNumber = null,
        string? shippedNotes = null,
        decimal? acceptedQuantityTons = null,
        decimal discountPercent = 0m,
        bool? specsMet = null,
        string? specsOutcomeNotes = null,
        bool requireNoActiveDispute = true,
        decimal? weighedQuantityTons = null,
        string? weighbridgeTicketUrl = null,
        GateRejectReason? gateRejectReason = null,
        string? gateRejectNotes = null)
    {
        if (asFarm && !FulfillmentTransitions.IsFarmAction(to))
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.Forbidden);
        if (!asFarm && !FulfillmentTransitions.IsFactoryAction(to))
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.Forbidden);

        if (acceptedQuantityTons is < 0)
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.InvalidQualityCheck);
        if (discountPercent is < 0 or > 100)
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.InvalidQualityCheck);

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
        if (fulfillment.Status == FulfillmentStatus.RejectedAtGate)
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.InvalidTransition);

        if (to == FulfillmentStatus.QualityChecked
            && fulfillment.WeighedQuantityTons is decimal weighedCap
            && acceptedQuantityTons is decimal accepted
            && accepted > weighedCap)
        {
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.AcceptedExceedsWeighed);
        }

        var from = fulfillment.Status;
        if (!FulfillmentTransitions.CanTransition(from, to))
            return Result<FulfillmentDto>.Failure(FulfillmentErrors.InvalidTransition);

        var returnFreight = to == FulfillmentStatus.RejectedAtGate
            ? DeliveryTermsPolicy.ReturnFreightBearer(fulfillment.DeliveryPoint)
            : (DealParty?)null;

        await using var tx = await _unitOfWork.BeginTransactionAsync();

        var ok = await _fulfillments.TryAtomicTransitionAsync(
            fulfillment.FulfillmentId,
            from,
            to,
            DateTime.UtcNow,
            qualityNotes,
            requireNoActiveDispute,
            carrier,
            trackingNumber,
            shippedNotes,
            acceptedQuantityTons,
            to == FulfillmentStatus.QualityChecked ? discountPercent : null,
            to == FulfillmentStatus.QualityChecked ? specsMet : null,
            to == FulfillmentStatus.QualityChecked ? specsOutcomeNotes : null,
            weighedQuantityTons,
            weighbridgeTicketUrl,
            gateRejectReason,
            gateRejectNotes,
            returnFreight);

        if (!ok)
        {
            var frozen = requireNoActiveDispute && await _disputes.HasActiveDisputeAsync(contractId);
            return Result<FulfillmentDto>.Failure(
                frozen ? FulfillmentErrors.FrozenByDispute : FulfillmentErrors.Conflict);
        }

        var eventNote = to switch
        {
            FulfillmentStatus.Shipped => BuildShipNote(carrier, trackingNumber, shippedNotes),
            FulfillmentStatus.Received =>
                $"Weighed {weighedQuantityTons:0.###} t"
                + (string.IsNullOrWhiteSpace(weighbridgeTicketUrl) ? "" : " · ticket attached"),
            FulfillmentStatus.QualityChecked => BuildQcNote(
                qualityNotes, acceptedQuantityTons, discountPercent, specsMet, specsOutcomeNotes),
            FulfillmentStatus.RejectedAtGate =>
                $"Rejected at gate: {gateRejectReason}"
                + (string.IsNullOrWhiteSpace(gateRejectNotes) ? "" : $" — {gateRejectNotes}")
                + $" · return freight: {returnFreight}",
            _ => qualityNotes
        };

        await _fulfillments.AddEventAsync(new FulfillmentEvent
        {
            EventId = Guid.NewGuid(),
            FulfillmentId = fulfillment.FulfillmentId,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = userId,
            Note = eventNote,
            CreatedAt = DateTime.UtcNow
        });

        if (to == FulfillmentStatus.Received && weighedQuantityTons is decimal weighed)
        {
            var contracted = contract.FarmMatch?.SupplyRequest?.QuantityTons ?? 0m;
            if (contracted > 0 && weighed < contracted)
            {
                var payable = Math.Min(weighed, contracted);
                var factor = payable / contracted;
                var scaled = await _milestones.TryScaleOpenMilestonesByFactorAsync(
                    contractId, factor, userId, DateTime.UtcNow);
                foreach (var (txId, prev, next) in scaled)
                {
                    var money = await _escrowPayments.ApplyQcAmountAdjustmentAsync(
                        contractId, txId, prev, next);
                    if (money.IsFailure)
                        return Result<FulfillmentDto>.Failure(money.Error!);
                }
            }
        }

        if (to == FulfillmentStatus.RejectedAtGate)
        {
            var refund = await _escrowPayments.RefundLeftoverDealHoldAsync(
                contractId, userId, "Rejected at gate");
            if (refund.IsFailure)
                return Result<FulfillmentDto>.Failure(refund.Error!);
        }

        if (to == FulfillmentStatus.QualityChecked)
        {
            var requestedQty = contract.FarmMatch?.SupplyRequest?.QuantityTons;
            var weighedAtReceive = fulfillment.WeighedQuantityTons;
            var qtyBaseline = weighedAtReceive is > 0 && requestedQty is > 0
                ? Math.Min(weighedAtReceive.Value, requestedQty.Value)
                : weighedAtReceive is > 0 ? weighedAtReceive.Value : requestedQty;

            if (discountPercent <= 0
                && acceptedQuantityTons is > 0
                && qtyBaseline is > 0
                && acceptedQuantityTons.Value < qtyBaseline.Value)
            {
                discountPercent = decimal.Round(
                    (1m - (acceptedQuantityTons.Value / qtyBaseline.Value)) * 100m,
                    2,
                    MidpointRounding.AwayFromZero);
                discountPercent = ClampDiscount(discountPercent);
            }

            if (discountPercent > 0)
            {
                var adjusted = await _milestones.TryApplyDiscountToFirstOpenMilestoneAsync(
                    contractId,
                    discountPercent,
                    userId,
                    DateTime.UtcNow);
                if (adjusted.TransactionId is Guid txId
                    && adjusted.PreviousAmount is decimal prev
                    && adjusted.NewAmount is decimal next
                    && next < prev)
                {
                    var money = await _escrowPayments.ApplyQcAmountAdjustmentAsync(
                        contractId, txId, prev, next);
                    if (money.IsFailure)
                        return Result<FulfillmentDto>.Failure(money.Error!);
                }
            }
        }

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

        if (to == FulfillmentStatus.Shipped && _outbound is not null)
        {
            var farmUserId = contract.FarmMatch?.Farm?.UserId;
            var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
            var target = asFarm ? factoryUserId : farmUserId;
            await _outbound.EnqueueWhatsAppAsync(
                target,
                null,
                ChannelTemplates.Shipped,
                "Shipment marked for your NileChain supply contract.",
                NotificationRelations.Contract,
                contract.ContractId);
        }

        var updated = await _fulfillments.GetByContractIdAsync(contractId);
        return Result<FulfillmentDto>.Success(Map(updated!));
    }

    private static decimal ClampDiscount(decimal value) =>
        value < 0 ? 0 : value > 100 ? 100 : value;

    private static string? BuildShipNote(string? carrier, string? tracking, string? notes)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(carrier)) parts.Add($"Carrier: {carrier}");
        if (!string.IsNullOrWhiteSpace(tracking)) parts.Add($"Tracking: {tracking}");
        if (!string.IsNullOrWhiteSpace(notes)) parts.Add(notes);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }

    private static string? BuildQcNote(
        string? notes,
        decimal? acceptedQty,
        decimal discount,
        bool? specsMet = null,
        string? specsOutcomeNotes = null)
    {
        var parts = new List<string>();
        if (acceptedQty is not null) parts.Add($"Accepted: {acceptedQty} t");
        if (discount > 0) parts.Add($"Discount: {discount:0.##}%");
        if (specsMet is not null) parts.Add(specsMet.Value ? "Specs: met" : "Specs: not met");
        if (!string.IsNullOrWhiteSpace(specsOutcomeNotes)) parts.Add(specsOutcomeNotes);
        if (!string.IsNullOrWhiteSpace(notes)) parts.Add(notes);
        return parts.Count == 0 ? null : string.Join(" · ", parts);
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
            FulfillmentStatus.RejectedAtGate => ("Load rejected at the factory gate", "FulfillmentRejectedAtGate"),
            _ => ("Fulfillment updated", "Fulfillment")
        };

        await _notifications.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = target.Value,
            Title = title,
            Message = $"Fulfillment status is now {to} for your supply contract.",
            Type = type,
            RelatedEntityType = NotificationRelations.Contract,
            RelatedEntityId = contract.ContractId,
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
        Carrier = f.Carrier,
        TrackingNumber = f.TrackingNumber,
        ShippedNotes = f.ShippedNotes,
        AcceptedQuantityTons = f.AcceptedQuantityTons,
        DiscountPercent = f.DiscountPercent,
        SpecsMet = f.SpecsMet,
        SpecsOutcomeNotes = f.SpecsOutcomeNotes,
        DeliveryPoint = f.DeliveryPoint.ToString(),
        FreightPayer = f.FreightPayer.ToString(),
        TransitRisk = f.TransitRisk.ToString(),
        ContractedQuantityTons = f.Contract?.FarmMatch?.SupplyRequest?.QuantityTons,
        WeighedQuantityTons = f.WeighedQuantityTons,
        WeighbridgeTicketUrl = f.WeighbridgeTicketUrl,
        RejectedAtGateAt = f.RejectedAtGateAt,
        GateRejectReason = f.GateRejectReason?.ToString(),
        GateRejectNotes = f.GateRejectNotes,
        ReturnFreightBearer = f.ReturnFreightBearer?.ToString(),
        RequestedQuality = StructuredQualitySpecs.Parse(
            f.Contract?.FarmMatch?.SupplyRequest?.QualitySpecs),
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
