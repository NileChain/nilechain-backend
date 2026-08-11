using NileChain.Application.Common;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Application.Services;

public class FactoryService : IFactoryService
{
    private readonly IFactoryRepository _factoryRepository;
    private readonly IRepository<CropType> _cropTypeRepository;
    private readonly IRepository<SupplyRequest> _supplyRequestRepository;
    private readonly IRepository<FarmMatch> _farmMatchRepository;
    private readonly IRepository<Contract> _contractRepository;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IContractPdfService _pdfService;
    private readonly IFulfillmentService _fulfillmentService;
    private readonly IPaymentMilestoneService _paymentMilestoneService;
    private readonly IDisputeService _disputeService;
    private readonly IUnitOfWork _unitOfWork;

    public FactoryService(
        IFactoryRepository factoryRepository,
        IRepository<CropType> cropTypeRepository,
        IRepository<SupplyRequest> supplyRequestRepository,
        IRepository<FarmMatch> farmMatchRepository,
        IRepository<Contract> contractRepository,
        IRepository<Message> messageRepository,
        IRepository<Notification> notificationRepository,
        IContractPdfService pdfService,
        IFulfillmentService fulfillmentService,
        IPaymentMilestoneService paymentMilestoneService,
        IDisputeService disputeService,
        IUnitOfWork unitOfWork)
    {
        _factoryRepository = factoryRepository;
        _cropTypeRepository = cropTypeRepository;
        _supplyRequestRepository = supplyRequestRepository;
        _farmMatchRepository = farmMatchRepository;
        _contractRepository = contractRepository;
        _messageRepository = messageRepository;
        _notificationRepository = notificationRepository;
        _pdfService = pdfService;
        _fulfillmentService = fulfillmentService;
        _paymentMilestoneService = paymentMilestoneService;
        _disputeService = disputeService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> RegisterFactoryAsync(Guid userId, string name, string governorate)
    {
        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Governorate = governorate
        };

        await _factoryRepository.AddAsync(factory);
        await _unitOfWork.SaveChangesAsync();
        return factory.FactoryId;
    }

    public async Task<Result<FactoryProfileResponse>> GetProfileAsync(Guid userId)
    {
        var factory = await _factoryRepository.GetFactoryWithDetailsAsync(userId);
        if (factory is null)
            return Result<FactoryProfileResponse>.Failure(FactoryErrors.FactoryNotFound);

        return Result<FactoryProfileResponse>.Success(MapToProfileResponse(factory));
    }

    public async Task<Result> UpdateProfileAsync(Guid userId, UpdateFactoryProfileRequest request)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result.Failure(FactoryErrors.FactoryNotFound);

        factory.Name = request.Name;
        factory.Location = request.Location;
        factory.Governorate = request.Governorate;
        factory.Latitude = request.Latitude;
        factory.Longitude = request.Longitude;
        factory.IndustryType = request.IndustryType;

        _factoryRepository.Update(factory);
        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<CreateSupplyRequestResponse>> CreateRequestAsync(
        Guid userId,
        CreateSupplyRequestRequest request,
        string? idempotencyKey = null)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<CreateSupplyRequestResponse>.Failure(FactoryErrors.FactoryNotFound);

        var key = NormalizeIdempotencyKey(idempotencyKey ?? request.IdempotencyKey);
        if (key is not null)
        {
            var existing = await _factoryRepository.GetSupplyRequestByIdempotencyKeyAsync(
                factory.FactoryId,
                key);
            if (existing is not null)
            {
                return Result<CreateSupplyRequestResponse>.Success(new CreateSupplyRequestResponse
                {
                    RequestId = existing.RequestId
                });
            }
        }

        var crops = await _cropTypeRepository.GetAllAsync();
        var crop = crops.FirstOrDefault(c =>
            string.Equals(c.Name, request.Crop, StringComparison.OrdinalIgnoreCase));
        if (crop is null)
            return Result<CreateSupplyRequestResponse>.Failure(FactoryErrors.CropTypeNotFound);

        var quality = request.Quality?.Trim() ?? string.Empty;

        // Normalize governorate slugs (giza → Giza) so MatchingPlugin hard filters work.
        var normalizedGovs = (request.SelectedGovernorates ?? new List<string>())
            .Select(NormalizeGovernorateLabel)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedGovs.Count > 0)
        {
            var govPart = string.Join(",", normalizedGovs);
            quality = string.IsNullOrWhiteSpace(quality)
                ? $"Gov:{govPart}"
                : $"{quality} | Gov:{govPart}";
        }

        var scope = string.IsNullOrWhiteSpace(request.GeographicScope)
            ? (normalizedGovs.Count > 0 || !string.IsNullOrWhiteSpace(factory.Governorate)
                ? "Exact"
                : "Nationwide")
            : request.GeographicScope.Trim();

        quality = string.IsNullOrWhiteSpace(quality)
            ? $"GeoScope:{scope}"
            : $"{quality} | GeoScope:{scope}";

        var entity = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = crop.CropTypeId,
            QuantityTons = request.Quantity,
            PricePerTon = request.Price,
            DeliveryDate = DeliveryDatePolicy.ToUtcStorage(request.DeliveryDate),
            QualitySpecs = quality,
            Status = SupplyRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            IdempotencyKey = key
        };

        await _supplyRequestRepository.AddAsync(entity);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException) when (key is not null)
        {
            // Race: another request with the same key won — return the winner.
            var winner = await _factoryRepository.GetSupplyRequestByIdempotencyKeyAsync(
                factory.FactoryId,
                key);
            if (winner is not null)
            {
                return Result<CreateSupplyRequestResponse>.Success(new CreateSupplyRequestResponse
                {
                    RequestId = winner.RequestId
                });
            }

            throw;
        }

        return Result<CreateSupplyRequestResponse>.Success(new CreateSupplyRequestResponse
        {
            RequestId = entity.RequestId
        });
    }

    public async Task<Result<PagedResult<FactorySupplyRequestListItemDto>>> GetRequestsAsync(
        Guid userId,
        int page = 1,
        int pageSize = 10)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<PagedResult<FactorySupplyRequestListItemDto>>.Failure(FactoryErrors.FactoryNotFound);

        var (items, total) = await _factoryRepository.GetSupplyRequestsPagedAsync(
            factory.FactoryId,
            page,
            pageSize);

        var dtos = items.Select(r => new FactorySupplyRequestListItemDto
        {
            RequestId = r.RequestId,
            Crop = r.CropType?.Name ?? string.Empty,
            QuantityTons = r.QuantityTons,
            PricePerTon = r.PricePerTon,
            DeliveryDate = r.DeliveryDate,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt,
            IdempotencyKey = r.IdempotencyKey
        }).ToList();

        return Result<PagedResult<FactorySupplyRequestListItemDto>>.Success(new PagedResult<FactorySupplyRequestListItemDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100)
        });
    }

    public async Task<Result<List<FactoryMatchItemDto>>> GetRequestMatchesAsync(
        Guid userId,
        Guid requestId,
        string? sort = null)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.FactoryNotFound);

        var supplyRequest = await _factoryRepository.GetSupplyRequestByIdAsync(requestId);
        if (supplyRequest is null)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.SupplyRequestNotFound);

        if (supplyRequest.FactoryId != factory.FactoryId)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.UnauthorizedAccess);

        // Ordering: MatchListOrdering — default CreatedAt DESC (newest first).
        var matches = await _factoryRepository.GetMatchesByRequestIdAsync(
            factory.FactoryId,
            requestId,
            sort);

        var dtos = matches.Select(m =>
        {
            double? distanceKm = null;
            var usedFallback = false;
            var farmLat = m.Farm?.Latitude;
            var farmLon = m.Farm?.Longitude;
            var factoryLat = factory.Latitude;
            var factoryLon = factory.Longitude;

            if (factoryLat is not null && factoryLon is not null
                && farmLat is not null && farmLon is not null)
            {
                distanceKm = Haversine.DistanceKm(
                    factoryLat.Value,
                    factoryLon.Value,
                    farmLat.Value,
                    farmLon.Value);
            }
            else
            {
                // Persisted shortlist may predate coords or profiles lack lat/long.
                usedFallback = true;
            }

            return new FactoryMatchItemDto
            {
                MatchId = m.MatchId,
                FarmId = m.FarmId,
                FarmName = m.Farm?.Name ?? "Unknown",
                FarmLocation = m.Farm?.Location,
                FarmGovernorate = m.Farm?.Governorate,
                FarmLatitude = farmLat,
                FarmLongitude = farmLon,
                FarmIsVerified = m.Farm?.IsVerified ?? false,
                FarmAverageRating = m.Farm?.AverageRating ?? 0,
                MatchScore = m.MatchScore,
                RiskScore = m.RiskScore,
                DistanceKm = distanceKm,
                UsedGovernorateFallback = usedFallback,
                Status = m.Status.ToString(),
                CreatedAt = m.CreatedAt
            };
        }).ToList();

        return Result<List<FactoryMatchItemDto>>.Success(dtos);
    }

    public async Task<Result> ExcludeMatchAsync(Guid userId, Guid matchId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result.Failure(FactoryErrors.FactoryNotFound);

        var match = await _factoryRepository.GetMatchForFactoryAsync(factory.FactoryId, matchId);
        if (match is null)
            return Result.Failure(FactoryErrors.MatchNotFound);

        if (match.Status != FarmMatchStatus.Proposed)
            return Result.Failure(FactoryErrors.MatchCannotExclude);

        match.Status = FarmMatchStatus.Rejected;
        _farmMatchRepository.Update(match);

        var contract = match.Contract;
        if (contract is not null &&
            contract.Status is ContractStatus.PendingSignature
                or ContractStatus.Draft
                or ContractStatus.PendingFarmSignature
                or ContractStatus.PendingFactorySignature)
        {
            contract.Status = ContractStatus.Cancelled;
            contract.ClearSignatures();
            _contractRepository.Update(contract);
        }

        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<List<FactoryMatchedFarmDto>>> GetMatchedFarmsAsync(Guid userId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<List<FactoryMatchedFarmDto>>.Failure(FactoryErrors.FactoryNotFound);

        var matches = await _factoryRepository.GetConversationsAsync(factory.FactoryId);

        var farms = matches
            .GroupBy(m => m.FarmId)
            .Select(g =>
            {
                // Prefer highest score for the farm row content; order list by most recent match event.
                var best = g
                    .OrderByDescending(m => m.MatchScore ?? 0)
                    .ThenByDescending(m => m.CreatedAt)
                    .First();
                var latestAt = g.Max(m => m.CreatedAt);
                return new FactoryMatchedFarmDto
                {
                    FarmId = best.FarmId,
                    FarmName = best.Farm?.Name ?? "Unknown",
                    RequestId = best.RequestId,
                    MatchId = best.MatchId,
                    MatchScore = best.MatchScore,
                    RiskScore = best.RiskScore,
                    FarmGovernorate = best.Farm?.Governorate,
                    CreatedAt = latestAt
                };
            })
            // Default list order: newest match event first (CreatedAt), then score.
            .OrderByDescending(f => f.CreatedAt)
            .ThenByDescending(f => f.MatchScore ?? 0)
            .ThenBy(f => f.FarmName)
            .ToList();

        return Result<List<FactoryMatchedFarmDto>>.Success(farms);
    }

    public async Task<Result<List<FactoryNotificationDto>>> GetNotificationsAsync(Guid userId)
    {
        var notifications = await _factoryRepository.GetNotificationsAsync(userId);
        var dtos = notifications.Select(n => new FactoryNotificationDto
        {
            NotificationId = n.NotificationId,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
        return Result<List<FactoryNotificationDto>>.Success(dtos);
    }

    public async Task<Result> MarkNotificationAsReadAsync(Guid userId, Guid notificationId)
    {
        var notifications = await _factoryRepository.GetNotificationsAsync(userId);
        var notification = notifications.FirstOrDefault(n => n.NotificationId == notificationId);
        if (notification is null)
            return Result.Failure(FactoryErrors.NotificationNotFound);

        notification.IsRead = true;
        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<List<FactoryConversationDto>>> GetConversationsAsync(Guid userId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<List<FactoryConversationDto>>.Failure(FactoryErrors.FactoryNotFound);

        var matches = await _factoryRepository.GetConversationsAsync(factory.FactoryId);
        var dtos = matches.Select(m =>
        {
            var lastMsg = m.Messages.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
            return new FactoryConversationDto
            {
                MatchId = m.MatchId,
                FarmName = m.Farm?.Name ?? "Unknown",
                CropName = m.SupplyRequest?.CropType?.Name,
                LastMessage = lastMsg?.Content,
                LastMessageAt = lastMsg?.CreatedAt,
                UnreadCount = m.Messages.Count(x => !x.IsRead && x.ReceiverId == factory.UserId)
            };
        }).OrderByDescending(d => d.LastMessageAt).ToList();

        return Result<List<FactoryConversationDto>>.Success(dtos);
    }

    public async Task<Result<List<FactoryMessageDto>>> GetMessagesAsync(Guid userId, Guid matchId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<List<FactoryMessageDto>>.Failure(FactoryErrors.FactoryNotFound);

        var match = await _factoryRepository.GetMatchForFactoryAsync(factory.FactoryId, matchId);
        if (match is null)
            return Result<List<FactoryMessageDto>>.Failure(FactoryErrors.ConversationNotFound);

        var messages = await _factoryRepository.GetMessagesAsync(factory.FactoryId, matchId);
        var dtos = messages.Select(m => new FactoryMessageDto
        {
            MessageId = m.MessageId,
            MatchId = m.MatchId,
            SenderId = m.SenderId,
            SenderName = m.Sender?.UserName ?? m.Sender?.Email ?? "Unknown",
            Content = m.Content,
            IsRead = m.IsRead,
            CreatedAt = m.CreatedAt
        }).ToList();

        return Result<List<FactoryMessageDto>>.Success(dtos);
    }

    public async Task<Result> SendMessageAsync(Guid userId, Guid matchId, string content)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result.Failure(FactoryErrors.FactoryNotFound);

        var match = await _factoryRepository.GetMatchForFactoryAsync(factory.FactoryId, matchId);
        if (match is null)
            return Result.Failure(FactoryErrors.ConversationNotFound);

        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure(FactoryErrors.InvalidAction);

        var message = new Message
        {
            MessageId = Guid.NewGuid(),
            MatchId = matchId,
            SenderId = userId,
            ReceiverId = match.Farm?.UserId ?? Guid.Empty,
            Content = content.Trim(),
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<PersistContractResponse>> PersistContractAsync(
        Guid userId,
        PersistContractRequest request)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<PersistContractResponse>.Failure(FactoryErrors.FactoryNotFound);

        var match = await _factoryRepository.GetMatchForFactoryAsync(factory.FactoryId, request.MatchId);
        if (match is null)
            return Result<PersistContractResponse>.Failure(FactoryErrors.MatchNotFound);

        if (string.IsNullOrWhiteSpace(request.ContractText))
            return Result<PersistContractResponse>.Failure(FactoryErrors.InvalidAction);

        if (!IsPartyActive(match.Farm?.User) || !IsPartyActive(match.SupplyRequest?.Factory?.User))
            return Result<PersistContractResponse>.Failure(FactoryErrors.PartyInactive);

        if (!MatchGovernoratePolicy.IsSnapshotStillValid(
                match.MatchedGovernorate,
                match.Farm?.Governorate,
                match.SupplyRequest?.QualitySpecs))
        {
            return Result<PersistContractResponse>.Failure(FactoryErrors.GovernorateMismatch);
        }

        Contract contract;
        var isCreate = match.Contract is null;
        var textChanged = false;

        if (match.Contract is not null)
        {
            contract = match.Contract;
            textChanged = !string.Equals(
                contract.GeneratedText,
                request.ContractText,
                StringComparison.Ordinal);

            if (textChanged)
            {
                // Active disputes block regen — rewriting text would erase the signed-deal
                // context under admin review (unlike void-on-regen for fulfillment/payments).
                if (await _disputeService.HasActiveDisputeAsync(contract.ContractId))
                    return Result<PersistContractResponse>.Failure(DisputeErrors.RegenBlocked);

                // Material text change invalidates signatures; Accepted match reopens to Proposed.
                // Rejected/Expired matches fail cleanly — never produce Rejected+Signed.
                if (!ContractExecution.TryReplaceGeneratedText(contract, match, request.ContractText))
                    return Result<PersistContractResponse>.Failure(FactoryErrors.MatchNotProposed);

                _contractRepository.Update(contract);
            }
        }
        else
        {
            if (!ContractExecution.CanCreateContract(match))
                return Result<PersistContractResponse>.Failure(FactoryErrors.MatchNotProposed);

            contract = new Contract
            {
                ContractId = Guid.NewGuid(),
                MatchId = match.MatchId,
                GeneratedText = request.ContractText,
                Status = ContractStatus.PendingSignature,
                CreatedAt = DateTime.UtcNow
            };
            await _contractRepository.AddAsync(contract);
            textChanged = true;
        }

        // Notify only on create or when GeneratedText actually changed.
        if (isCreate || textChanged)
        {
            var farmUserId = match.Farm?.UserId ?? Guid.Empty;
            if (farmUserId != Guid.Empty)
            {
                await _notificationRepository.AddAsync(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = farmUserId,
                    Title = "Contract ready for signature",
                    Message = $"A supply contract from {factory.Name} is ready for review.",
                    Type = "ContractReady",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        await _unitOfWork.SaveChangesAsync();

        if (textChanged && !isCreate)
        {
            await _fulfillmentService.VoidForContractAsync(
                contract.ContractId,
                userId,
                "Contract text regenerated — prior fulfillment voided");
            await _paymentMilestoneService.VoidForContractAsync(
                contract.ContractId,
                userId,
                "Contract text regenerated — prior payment milestone schedule voided");
        }

        return Result<PersistContractResponse>.Success(new PersistContractResponse
        {
            ContractId = contract.ContractId,
            Status = contract.Status.ToString()
        });
    }

    public async Task<Result<List<FactoryContractDto>>> GetContractsAsync(Guid userId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<List<FactoryContractDto>>.Failure(FactoryErrors.FactoryNotFound);

        var contracts = await _factoryRepository.GetContractsAsync(factory.FactoryId);
        return Result<List<FactoryContractDto>>.Success(contracts.Select(MapContract).ToList());
    }

    public async Task<Result<FactoryContractDto>> GetContractAsync(Guid userId, Guid contractId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<FactoryContractDto>.Failure(FactoryErrors.FactoryNotFound);

        var contract = await _factoryRepository.GetContractForFactoryAsync(factory.FactoryId, contractId);
        if (contract is null)
            return Result<FactoryContractDto>.Failure(FactoryErrors.ContractNotFound);

        return Result<FactoryContractDto>.Success(MapContract(contract));
    }

    public async Task<Result<FactoryContractDto>> ApproveContractAsync(Guid userId, Guid contractId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<FactoryContractDto>.Failure(FactoryErrors.FactoryNotFound);

        var contract = await _factoryRepository.GetContractForFactoryAsync(factory.FactoryId, contractId);
        if (contract is null)
            return Result<FactoryContractDto>.Failure(FactoryErrors.ContractNotFound);

        if (contract.Status == ContractStatus.Cancelled)
            return Result<FactoryContractDto>.Failure(FactoryErrors.ContractNotPending);

        var match = contract.FarmMatch;
        if (!IsPartyActive(match?.Farm?.User) || !IsPartyActive(match?.SupplyRequest?.Factory?.User))
            return Result<FactoryContractDto>.Failure(FactoryErrors.PartyInactive);

        if (!MatchGovernoratePolicy.IsSnapshotStillValid(
                match?.MatchedGovernorate,
                match?.Farm?.Governorate,
                match?.SupplyRequest?.QualitySpecs))
        {
            return Result<FactoryContractDto>.Failure(FactoryErrors.GovernorateMismatch);
        }

        if (!ContractExecution.CanSign(match))
            return Result<FactoryContractDto>.Failure(FactoryErrors.MatchNotProposed);

        // Idempotent: factory already signed — do not touch farm signature.
        if (contract.IsFactorySigned)
            return Result<FactoryContractDto>.Success(MapContract(contract));

        if (contract.Status is not (
                ContractStatus.Draft
                or ContractStatus.PendingSignature
                or ContractStatus.PendingFactorySignature))
        {
            return Result<FactoryContractDto>.Failure(FactoryErrors.ContractNotPending);
        }

        contract.FactorySignedAt = DateTime.UtcNow;
        // FarmSignedAt must remain unchanged.
        contract.RefreshSignatureStatus();
        ContractExecution.AcceptMatchIfFullySigned(contract);
        _contractRepository.Update(contract);

        var farmUserId = contract.FarmMatch?.Farm?.UserId;
        if (farmUserId is Guid uid && uid != Guid.Empty)
        {
            var fullySigned = contract.IsFullySigned;
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = fullySigned ? "Contract fully signed" : "Contract awaiting your signature",
                Message = fullySigned
                    ? $"{factory.Name} signed the supply contract. Both parties have now signed."
                    : $"{factory.Name} signed the supply contract. Your farm signature is still required.",
                Type = "Contract",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        var approveSaved = await TrySaveFactoryContractAsync();
        if (approveSaved.IsFailure)
            return Result<FactoryContractDto>.Failure(approveSaved.Error!);

        if (contract.IsFullySigned)
        {
            await _fulfillmentService.EnsureCreatedForSignedContractAsync(
                contract.ContractId,
                userId,
                contract.FarmMatch?.SupplyRequest?.DeliveryDate);
            await _paymentMilestoneService.EnsureCreatedForSignedContractAsync(
                contract.ContractId,
                userId,
                contract.FarmMatch?.SupplyRequest);
        }

        return Result<FactoryContractDto>.Success(MapContract(contract));
    }

    public async Task<Result<FactoryContractDto>> RejectContractAsync(Guid userId, Guid contractId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<FactoryContractDto>.Failure(FactoryErrors.FactoryNotFound);

        var contract = await _factoryRepository.GetContractForFactoryAsync(factory.FactoryId, contractId);
        if (contract is null)
            return Result<FactoryContractDto>.Failure(FactoryErrors.ContractNotFound);

        if (contract.Status == ContractStatus.Signed || contract.Status == ContractStatus.Cancelled)
            return Result<FactoryContractDto>.Failure(FactoryErrors.ContractNotPending);

        contract.Status = ContractStatus.Cancelled;
        contract.ClearSignatures();
        ContractExecution.RejectMatchIfProposed(contract);
        _contractRepository.Update(contract);

        var farmUserId = contract.FarmMatch?.Farm?.UserId;
        if (farmUserId is Guid uid && uid != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = "Contract cancelled",
                Message = $"{factory.Name} cancelled the supply contract.",
                Type = "Contract",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        var rejectSaved = await TrySaveFactoryContractAsync();
        if (rejectSaved.IsFailure)
            return Result<FactoryContractDto>.Failure(rejectSaved.Error!);

        await _fulfillmentService.VoidForContractAsync(
            contract.ContractId,
            userId,
            "Contract cancelled by factory");
        await _paymentMilestoneService.VoidForContractAsync(
            contract.ContractId,
            userId,
            "Contract cancelled by factory — payment milestone schedule voided");

        return Result<FactoryContractDto>.Success(MapContract(contract));
    }

    public async Task<Result<(byte[] PdfBytes, string FileName)>> GetContractPdfAsync(
        Guid userId,
        Guid contractId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<(byte[], string)>.Failure(FactoryErrors.FactoryNotFound);

        var contract = await _factoryRepository.GetContractForFactoryAsync(factory.FactoryId, contractId);
        if (contract is null)
            return Result<(byte[], string)>.Failure(FactoryErrors.ContractNotFound);

        var text = contract.GeneratedText ?? string.Empty;
        var farmName = contract.FarmMatch?.Farm?.Name ?? "Farm";
        var factoryName = contract.FarmMatch?.SupplyRequest?.Factory?.Name ?? factory.Name;
        var bytes = _pdfService.GeneratePdf(
            "Agricultural Supply Contract",
            text,
            farmName,
            factoryName,
            factorySigned: contract.IsFactorySigned,
            farmSigned: contract.IsFarmSigned,
            factorySignedAt: contract.FactorySignedAt,
            farmSignedAt: contract.FarmSignedAt);

        // Do not persist role-scoped PdfUrl — download endpoints are authoritative.
        return Result<(byte[], string)>.Success((bytes, $"contract-{contract.ContractId:N}.pdf"));
    }

    private static FactoryContractDto MapContract(Contract c) => new()
    {
        ContractId = c.ContractId,
        MatchId = c.MatchId,
        FarmName = c.FarmMatch?.Farm?.Name ?? "Unknown",
        FarmLocation = c.FarmMatch?.Farm?.Location ?? c.FarmMatch?.Farm?.Governorate,
        FactoryName = c.FarmMatch?.SupplyRequest?.Factory?.Name ?? "Unknown",
        CropName = c.FarmMatch?.SupplyRequest?.CropType?.Name,
        QuantityTons = c.FarmMatch?.SupplyRequest?.QuantityTons ?? 0,
        PricePerTon = c.FarmMatch?.SupplyRequest?.PricePerTon,
        DeliveryDate = c.FarmMatch?.SupplyRequest?.DeliveryDate,
        DeliveryLocation = c.FarmMatch?.SupplyRequest?.Factory?.Location
            ?? c.FarmMatch?.SupplyRequest?.Factory?.Governorate,
        GeneratedText = c.GeneratedText,
        PdfUrl = c.PdfUrl,
        Status = c.Status.ToString(),
        CreatedAt = c.CreatedAt,
        SignedAt = c.SignedAt,
        FactorySigned = c.IsFactorySigned,
        FarmSigned = c.IsFarmSigned,
        FactorySignedAt = c.FactorySignedAt,
        FarmSignedAt = c.FarmSignedAt,
        UpdatedAt = c.SignedAt ?? c.FarmSignedAt ?? c.FactorySignedAt ?? c.CreatedAt,
        MatchScore = c.FarmMatch?.MatchScore,
        RiskScore = c.FarmMatch?.RiskScore
    };

    /// <summary>
    /// Missing User navigation (tests/partial loads) is treated as active; explicit IsActive=false fails.
    /// </summary>
    private static bool IsPartyActive(NileChain.Domain.Identity.ApplicationUser? user) =>
        user is null || user.IsActive;

    /// <summary>Maps common UI slugs / aliases to canonical governorate names.</summary>
    private static string? NormalizeGovernorateLabel(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var key = raw.Trim().ToLowerInvariant();
        return key switch
        {
            "alex" or "alexandria" => "Alexandria",
            "giza" => "Giza",
            "cairo" => "Cairo",
            "beheira" => "Beheira",
            "minya" => "Minya",
            "luxor" => "Luxor",
            "sharqia" => "Sharqia",
            "assiut" or "asyut" => "Asyut",
            "fayoum" or "faiyum" => "Faiyum",
            _ => char.ToUpperInvariant(raw.Trim()[0]) + raw.Trim()[1..]
        };
    }

    private static FactoryProfileResponse MapToProfileResponse(Factory factory) => new()
    {
        FactoryId = factory.FactoryId,
        Name = factory.Name,
        Location = factory.Location,
        Governorate = factory.Governorate,
        Latitude = factory.Latitude,
        Longitude = factory.Longitude,
        IndustryType = factory.IndustryType,
        Phone = factory.User.PhoneNumber,
        IsVerified = factory.IsVerified,
        AverageRating = factory.AverageRating,
        RatingCount = factory.RatingCount,
        CompletionPercent = CalculateCompletionPercent(factory)
    };

    private static int CalculateCompletionPercent(Factory factory)
    {
        var fields = 0;
        if (!string.IsNullOrWhiteSpace(factory.Name)) fields++;
        if (!string.IsNullOrWhiteSpace(factory.Location)) fields++;
        if (!string.IsNullOrWhiteSpace(factory.Governorate)) fields++;
        if (!string.IsNullOrWhiteSpace(factory.IndustryType)) fields++;
        if (!string.IsNullOrWhiteSpace(factory.User.PhoneNumber)) fields++;
        return (int)Math.Round((fields / 5.0) * 100);
    }

    private async Task<Result> TrySaveFactoryContractAsync()
    {
        try
        {
            await _unitOfWork.SaveChangesAsync();
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(FactoryErrors.ConcurrencyConflict);
        }
    }

    private static string? NormalizeIdempotencyKey(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        return trimmed.Length > 128 ? trimmed[..128] : trimmed;
    }
}
