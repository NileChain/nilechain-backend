using NileChain.Application.Common;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class FactoryService : IFactoryService
{
    private readonly IFactoryRepository _factoryRepository;
    private readonly IRepository<CropType> _cropTypeRepository;
    private readonly IRepository<SupplyRequest> _supplyRequestRepository;
    private readonly IRepository<Contract> _contractRepository;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IContractPdfService _pdfService;
    private readonly IUnitOfWork _unitOfWork;

    public FactoryService(
        IFactoryRepository factoryRepository,
        IRepository<CropType> cropTypeRepository,
        IRepository<SupplyRequest> supplyRequestRepository,
        IRepository<Contract> contractRepository,
        IRepository<Message> messageRepository,
        IRepository<Notification> notificationRepository,
        IContractPdfService pdfService,
        IUnitOfWork unitOfWork)
    {
        _factoryRepository = factoryRepository;
        _cropTypeRepository = cropTypeRepository;
        _supplyRequestRepository = supplyRequestRepository;
        _contractRepository = contractRepository;
        _messageRepository = messageRepository;
        _notificationRepository = notificationRepository;
        _pdfService = pdfService;
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
        CreateSupplyRequestRequest request)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<CreateSupplyRequestResponse>.Failure(FactoryErrors.FactoryNotFound);

        var crops = await _cropTypeRepository.GetAllAsync();
        var crop = crops.FirstOrDefault(c =>
            string.Equals(c.Name, request.Crop, StringComparison.OrdinalIgnoreCase));
        if (crop is null)
            return Result<CreateSupplyRequestResponse>.Failure(FactoryErrors.CropTypeNotFound);

        var quality = request.Quality?.Trim() ?? string.Empty;
        if (request.SelectedGovernorates is { Count: > 0 })
        {
            var govPart = string.Join(",", request.SelectedGovernorates);
            quality = string.IsNullOrWhiteSpace(quality)
                ? $"Gov:{govPart}"
                : $"{quality} | Gov:{govPart}";
        }

        var entity = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = crop.CropTypeId,
            QuantityTons = request.Quantity,
            PricePerTon = request.Price,
            DeliveryDate = DateTime.SpecifyKind(request.DeliveryDate.Date, DateTimeKind.Utc),
            QualitySpecs = quality,
            Status = SupplyRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _supplyRequestRepository.AddAsync(entity);
        await _unitOfWork.SaveChangesAsync();

        return Result<CreateSupplyRequestResponse>.Success(new CreateSupplyRequestResponse
        {
            RequestId = entity.RequestId
        });
    }

    public async Task<Result<List<FactoryMatchItemDto>>> GetRequestMatchesAsync(Guid userId, Guid requestId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.FactoryNotFound);

        var supplyRequest = await _factoryRepository.GetSupplyRequestByIdAsync(requestId);
        if (supplyRequest is null)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.SupplyRequestNotFound);

        if (supplyRequest.FactoryId != factory.FactoryId)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.UnauthorizedAccess);

        var matches = await _factoryRepository.GetMatchesByRequestIdAsync(factory.FactoryId, requestId);

        var dtos = matches.Select(m => new FactoryMatchItemDto
        {
            MatchId = m.MatchId,
            FarmId = m.FarmId,
            FarmName = m.Farm?.Name ?? "Unknown",
            FarmLocation = m.Farm?.Location,
            FarmGovernorate = m.Farm?.Governorate,
            FarmLatitude = m.Farm?.Latitude,
            FarmLongitude = m.Farm?.Longitude,
            FarmIsVerified = m.Farm?.IsVerified ?? false,
            FarmAverageRating = m.Farm?.AverageRating ?? 0,
            MatchScore = m.MatchScore,
            RiskScore = m.RiskScore,
            Status = m.Status.ToString(),
            CreatedAt = m.CreatedAt
        }).ToList();

        return Result<List<FactoryMatchItemDto>>.Success(dtos);
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
                var best = g
                    .OrderByDescending(m => m.MatchScore ?? 0)
                    .ThenByDescending(m => m.CreatedAt)
                    .First();
                return new FactoryMatchedFarmDto
                {
                    FarmId = best.FarmId,
                    FarmName = best.Farm?.Name ?? "Unknown",
                    RequestId = best.RequestId,
                    MatchId = best.MatchId,
                    MatchScore = best.MatchScore,
                    RiskScore = best.RiskScore,
                    FarmGovernorate = best.Farm?.Governorate
                };
            })
            .OrderByDescending(f => f.MatchScore ?? 0)
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

        Contract contract;
        if (match.Contract is not null)
        {
            contract = match.Contract;
            contract.GeneratedText = request.ContractText;
            if (contract.Status == ContractStatus.Cancelled)
                contract.Status = ContractStatus.Draft;
            _contractRepository.Update(contract);
        }
        else
        {
            contract = new Contract
            {
                ContractId = Guid.NewGuid(),
                MatchId = match.MatchId,
                GeneratedText = request.ContractText,
                Status = ContractStatus.PendingSignature,
                CreatedAt = DateTime.UtcNow
            };
            await _contractRepository.AddAsync(contract);
        }

        await _notificationRepository.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = match.Farm?.UserId ?? Guid.Empty,
            Title = "Contract ready for signature",
            Message = $"A supply contract from {factory.Name} is ready for review.",
            Type = "Contract",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync();

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

        contract.Status = ContractStatus.Signed;
        contract.SignedAt = DateTime.UtcNow;
        _contractRepository.Update(contract);

        var farmUserId = contract.FarmMatch?.Farm?.UserId;
        if (farmUserId is Guid uid && uid != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = "Contract signed",
                Message = $"{factory.Name} signed the supply contract.",
                Type = "Contract",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
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

        contract.Status = ContractStatus.Cancelled;
        contract.SignedAt = null;
        _contractRepository.Update(contract);
        await _unitOfWork.SaveChangesAsync();
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
            contract.Status == ContractStatus.Signed);

        contract.PdfUrl = $"/api/factory/contracts/{contract.ContractId}/pdf";
        _contractRepository.Update(contract);
        await _unitOfWork.SaveChangesAsync();

        return Result<(byte[], string)>.Success((bytes, $"contract-{contract.ContractId:N}.pdf"));
    }

    private static FactoryContractDto MapContract(Contract c) => new()
    {
        ContractId = c.ContractId,
        MatchId = c.MatchId,
        FarmName = c.FarmMatch?.Farm?.Name ?? "Unknown",
        CropName = c.FarmMatch?.SupplyRequest?.CropType?.Name,
        QuantityTons = c.FarmMatch?.SupplyRequest?.QuantityTons ?? 0,
        PricePerTon = c.FarmMatch?.SupplyRequest?.PricePerTon,
        GeneratedText = c.GeneratedText,
        PdfUrl = c.PdfUrl,
        Status = c.Status.ToString(),
        CreatedAt = c.CreatedAt,
        SignedAt = c.SignedAt
    };

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
}
