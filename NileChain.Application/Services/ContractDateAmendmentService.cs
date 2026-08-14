using NileChain.Application.Common;
using NileChain.Application.Dtos.Contracts;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class ContractDateAmendmentService : IContractDateAmendmentService
{
    private readonly IFactoryRepository _factoryRepository;
    private readonly IFarmRepository _farmRepository;
    private readonly IRepository<Contract> _contractRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IContractIntegrityService _integrity;

    public ContractDateAmendmentService(
        IFactoryRepository factoryRepository,
        IFarmRepository farmRepository,
        IRepository<Contract> contractRepository,
        IRepository<Notification> notificationRepository,
        IUnitOfWork unitOfWork,
        IContractIntegrityService integrity)
    {
        _factoryRepository = factoryRepository;
        _farmRepository = farmRepository;
        _contractRepository = contractRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _integrity = integrity;
    }

    public async Task<Result<ContractDateAmendmentDto>> ProposeAsync(
        Guid userId,
        Guid contractId,
        bool asFactory,
        ProposeContractDateAmendmentRequest request)
    {
        var loaded = await LoadAsync(userId, contractId, asFactory);
        if (loaded.IsFailure)
            return Result<ContractDateAmendmentDto>.Failure(loaded.Error!);

        var (contract, counterpartyUserId) = loaded.Value!;
        if (!contract.IsFullySigned)
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.ContractNotSigned);

        if (request.StartsAt is null && request.EndsAt is null)
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.DateAmendmentInvalid);

        var nextStart = request.StartsAt.HasValue
            ? ContractTermDates.NormalizeDate(request.StartsAt.Value)
            : contract.StartsAt;
        var nextEnd = request.EndsAt.HasValue
            ? ContractTermDates.NormalizeDate(request.EndsAt.Value)
            : contract.EndsAt;

        if (nextStart.HasValue && nextEnd.HasValue && nextEnd.Value.Date < nextStart.Value.Date)
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.DateAmendmentEndBeforeStart);

        if (contract.HasPendingDateAmendment
            && contract.DateAmendmentProposedByUserId != userId)
        {
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.DateAmendmentPending);
        }

        ContractTermDates.ProposeAmendment(
            contract,
            userId,
            request.StartsAt,
            request.EndsAt);

        _contractRepository.Update(contract);

        if (counterpartyUserId is Guid other && other != Guid.Empty)
        {
            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "A contract date change was proposed."
                : request.Reason.Trim();
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = other,
                Title = "Contract date amendment proposed",
                Message = reason,
                Type = "ContractDateAmendment",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<ContractDateAmendmentDto>.Success(Map(contract, userId));
    }

    public async Task<Result<ContractDateAmendmentDto>> AcceptAsync(
        Guid userId,
        Guid contractId,
        bool asFactory)
    {
        var loaded = await LoadAsync(userId, contractId, asFactory);
        if (loaded.IsFailure)
            return Result<ContractDateAmendmentDto>.Failure(loaded.Error!);

        var (contract, counterpartyUserId) = loaded.Value!;
        if (!contract.HasPendingDateAmendment)
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.DateAmendmentNotPending);

        if (contract.DateAmendmentProposedByUserId == userId)
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.DateAmendmentOwnProposal);

        var pendingStart = contract.PendingStartsAt ?? contract.StartsAt;
        var pendingEnd = contract.PendingEndsAt ?? contract.EndsAt;
        if (pendingStart.HasValue && pendingEnd.HasValue
            && pendingEnd.Value.Date < pendingStart.Value.Date)
        {
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.DateAmendmentEndBeforeStart);
        }

        ContractTermDates.ApplyAmendment(
            contract,
            contract.PendingStartsAt,
            contract.PendingEndsAt);

        if (contract.EndsAt.HasValue && contract.FarmMatch is not null)
            contract.FarmMatch.CounterDeliveryDate = contract.EndsAt;

        if (contract.EndsAt.HasValue && contract.Fulfillment is not null)
            contract.Fulfillment.PlannedShipDate = contract.EndsAt;

        await _integrity.SupersedeActiveAsync(contract.ContractId);
        _contractRepository.Update(contract);

        if (counterpartyUserId is Guid other && other != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = other,
                Title = "Contract date amendment accepted",
                Message = "The other party accepted the proposed contract date change.",
                Type = "ContractDateAmendment",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
        if (contract.IsFullySigned)
            await _integrity.AnchorIfFullySignedAsync(contract);

        return Result<ContractDateAmendmentDto>.Success(Map(contract, userId));
    }

    public async Task<Result<ContractDateAmendmentDto>> RejectAsync(
        Guid userId,
        Guid contractId,
        bool asFactory)
    {
        var loaded = await LoadAsync(userId, contractId, asFactory);
        if (loaded.IsFailure)
            return Result<ContractDateAmendmentDto>.Failure(loaded.Error!);

        var (contract, counterpartyUserId) = loaded.Value!;
        if (!contract.HasPendingDateAmendment)
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.DateAmendmentNotPending);

        if (contract.DateAmendmentProposedByUserId == userId)
            return Result<ContractDateAmendmentDto>.Failure(FactoryErrors.DateAmendmentOwnProposal);

        ContractTermDates.ClearPendingAmendment(contract);
        _contractRepository.Update(contract);

        if (counterpartyUserId is Guid other && other != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = other,
                Title = "Contract date amendment rejected",
                Message = "The other party rejected the proposed contract date change.",
                Type = "ContractDateAmendment",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return Result<ContractDateAmendmentDto>.Success(Map(contract, userId));
    }

    public static ContractDateAmendmentDto Map(Contract contract, Guid viewerUserId) =>
        new()
        {
            StartsAt = contract.StartsAt,
            EndsAt = contract.EndsAt,
            PendingStartsAt = contract.PendingStartsAt,
            PendingEndsAt = contract.PendingEndsAt,
            ProposedByUserId = contract.DateAmendmentProposedByUserId,
            ProposedAt = contract.DateAmendmentProposedAt,
            HasPendingAmendment = contract.HasPendingDateAmendment,
            ProposedByMe = contract.DateAmendmentProposedByUserId == viewerUserId,
            CanRespond = contract.HasPendingDateAmendment
                && contract.DateAmendmentProposedByUserId != viewerUserId
        };

    private async Task<Result<(Contract Contract, Guid? CounterpartyUserId)>> LoadAsync(
        Guid userId,
        Guid contractId,
        bool asFactory)
    {
        if (asFactory)
        {
            var factory = await _factoryRepository.GetByUserIdAsync(userId);
            if (factory is null)
                return Result<(Contract, Guid?)>.Failure(FactoryErrors.FactoryNotFound);

            var contract = await _factoryRepository.GetContractForFactoryAsync(
                factory.FactoryId,
                contractId);
            if (contract is null)
                return Result<(Contract, Guid?)>.Failure(FactoryErrors.ContractNotFound);

            return Result<(Contract, Guid?)>.Success(
                (contract, contract.FarmMatch?.Farm?.UserId));
        }

        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<(Contract, Guid?)>.Failure(FarmErrors.FarmNotFound);

        var farmContract = await _farmRepository.GetContractForFarmAsync(userId, contractId);
        if (farmContract is null)
            return Result<(Contract, Guid?)>.Failure(FarmErrors.ContractNotFound);

        return Result<(Contract, Guid?)>.Success(
            (farmContract, farmContract.FarmMatch?.SupplyRequest?.Factory?.UserId));
    }
}
