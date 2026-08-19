using NileChain.Application.Common;
using NileChain.Application.Dtos.Contracts;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Notifications;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class ContractChangeRequestService : IContractChangeRequestService
{
    private readonly IFactoryRepository _factoryRepository;
    private readonly IFarmRepository _farmRepository;
    private readonly IRepository<Contract> _contractRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IRepository<ContractRevision> _revisionRepository;
    private readonly IDisputeService _disputeService;
    private readonly IContractTextReviser _textReviser;
    private readonly IFulfillmentService _fulfillmentService;
    private readonly IUnitOfWork _unitOfWork;

    public ContractChangeRequestService(
        IFactoryRepository factoryRepository,
        IFarmRepository farmRepository,
        IRepository<Contract> contractRepository,
        IRepository<Notification> notificationRepository,
        IRepository<ContractRevision> revisionRepository,
        IDisputeService disputeService,
        IContractTextReviser textReviser,
        IFulfillmentService fulfillmentService,
        IUnitOfWork unitOfWork)
    {
        _factoryRepository = factoryRepository;
        _farmRepository = farmRepository;
        _contractRepository = contractRepository;
        _notificationRepository = notificationRepository;
        _revisionRepository = revisionRepository;
        _disputeService = disputeService;
        _textReviser = textReviser;
        _fulfillmentService = fulfillmentService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RequestContractChangesResponse>> RequestChangesAsync(
        Guid userId,
        Guid contractId,
        bool asFactory,
        RequestContractChangesRequest request,
        CancellationToken cancellationToken = default)
    {
        var instructions = (request.Instructions ?? string.Empty).Trim();
        if (instructions.Length < 3)
            return Result<RequestContractChangesResponse>.Failure(FactoryErrors.ChangeInstructionsRequired);

        var loaded = await LoadAsync(userId, contractId, asFactory);
        if (loaded.IsFailure)
            return Result<RequestContractChangesResponse>.Failure(loaded.Error!);

        var (contract, counterpartyUserId, actorName) = loaded.Value!;

        if (contract.Status == ContractStatus.Cancelled)
            return Result<RequestContractChangesResponse>.Failure(FactoryErrors.ContractNotPending);

        if (contract.IsFullySigned)
            return Result<RequestContractChangesResponse>.Failure(FactoryErrors.ContractAlreadyFullySigned);

        if (string.IsNullOrWhiteSpace(contract.GeneratedText))
            return Result<RequestContractChangesResponse>.Failure(FactoryErrors.ContractTextMissing);

        var match = contract.FarmMatch;
        if (match is null || !ContractExecution.CanReplaceText(match))
            return Result<RequestContractChangesResponse>.Failure(FactoryErrors.MatchNotProposed);

        if (await _disputeService.HasActiveDisputeAsync(contract.ContractId))
            return Result<RequestContractChangesResponse>.Failure(DisputeErrors.RegenBlocked);

        var revised = await _textReviser.ReviseAsync(
            contract.GeneratedText,
            instructions,
            cancellationToken);
        if (revised.IsFailure)
            return Result<RequestContractChangesResponse>.Failure(
                revised.Error ?? FactoryErrors.AiRevisionUnavailable);

        var previousText = contract.GeneratedText ?? string.Empty;
        var hadSignatures = contract.IsFactorySigned || contract.IsFarmSigned;
        if (!ContractExecution.TryReplaceGeneratedText(contract, match, revised.Value!))
            return Result<RequestContractChangesResponse>.Failure(FactoryErrors.MatchNotProposed);

        var revision = new ContractRevision
        {
            ContractRevisionId = Guid.NewGuid(),
            ContractId = contract.ContractId,
            PreviousText = previousText,
            NewText = contract.GeneratedText ?? string.Empty,
            Instructions = instructions,
            RevisedByUserId = userId,
            RevisedByParty = asFactory ? DisputeParty.Factory : DisputeParty.Farm,
            CreatedAt = DateTime.UtcNow
        };
        await _revisionRepository.AddAsync(revision);

        _contractRepository.Update(contract);

        if (counterpartyUserId is Guid other && other != Guid.Empty)
        {
            var preview = instructions.Length > 280
                ? instructions[..277] + "…"
                : instructions;
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = other,
                Title = "Contract draft revised",
                Message = $"{actorName} requested changes and the draft was updated: {preview}",
                Type = "ContractRevision",
                RelatedEntityType = NotificationRelations.Contract,
                RelatedEntityId = contract.ContractId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();

        if (hadSignatures)
        {
            await _fulfillmentService.VoidForContractAsync(
                contract.ContractId,
                userId,
                "Contract draft revised before full signature — prior fulfillment voided");
        }

        return Result<RequestContractChangesResponse>.Success(new RequestContractChangesResponse
        {
            ContractId = contract.ContractId,
            GeneratedText = contract.GeneratedText ?? string.Empty,
            Status = contract.Status.ToString(),
            LastRevision = ContractRevisionDto.From(revision)
        });
    }

    private async Task<Result<(Contract Contract, Guid? CounterpartyUserId, string ActorName)>> LoadAsync(
        Guid userId,
        Guid contractId,
        bool asFactory)
    {
        if (asFactory)
        {
            var factory = await _factoryRepository.GetByUserIdAsync(userId);
            if (factory is null)
                return Result<(Contract, Guid?, string)>.Failure(FactoryErrors.FactoryNotFound);

            var contract = await _factoryRepository.GetContractForFactoryAsync(
                factory.FactoryId,
                contractId);
            if (contract is null)
                return Result<(Contract, Guid?, string)>.Failure(FactoryErrors.ContractNotFound);

            return Result<(Contract, Guid?, string)>.Success(
                (contract, contract.FarmMatch?.Farm?.UserId, factory.Name));
        }

        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<(Contract, Guid?, string)>.Failure(FarmErrors.FarmNotFound);

        var farmContract = await _farmRepository.GetContractForFarmAsync(userId, contractId);
        if (farmContract is null)
            return Result<(Contract, Guid?, string)>.Failure(FarmErrors.ContractNotFound);

        return Result<(Contract, Guid?, string)>.Success(
            (farmContract,
                farmContract.FarmMatch?.SupplyRequest?.Factory?.UserId,
                farm.Name));
    }
}
