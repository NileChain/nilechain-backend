using NileChain.Application.Common;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace NileChain.Application.Services;

public class FarmService : IFarmService
{
    private readonly IFarmRepository _farmRepository;
    private readonly IRepository<CropType> _cropTypeRepository;
    private readonly IRepository<FarmDocument> _farmDocumentRepository;
    private readonly IRepository<FarmMatch> _farmMatchRepository;
    private readonly IRepository<Message> _messageRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IUnitOfWork _unitOfWork;

    public FarmService(
        IFarmRepository farmRepository,
        IRepository<CropType> cropTypeRepository,
        IRepository<FarmDocument> farmDocumentRepository,
        IRepository<FarmMatch> farmMatchRepository,
        IRepository<Message> messageRepository,
        ICloudinaryService cloudinaryService,
        IUnitOfWork unitOfWork)
    {
        _farmRepository = farmRepository;
        _cropTypeRepository = cropTypeRepository;
        _farmDocumentRepository = farmDocumentRepository;
        _farmMatchRepository = farmMatchRepository;
        _messageRepository = messageRepository;
        _cloudinaryService = cloudinaryService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<FarmDashboardResponse>> GetDashboardAsync(Guid userId)
    {
        var farm = await _farmRepository.GetFarmWithDashboardDataAsync(userId);
        if (farm is null)
            return Result<FarmDashboardResponse>.Failure(FarmErrors.FarmNotFound);

        var matches = farm.FarmMatches ?? new List<FarmMatch>();
        var activeMatches = matches
            .Where(m => m.Status != FarmMatchStatus.Rejected && m.Status != FarmMatchStatus.Expired)
            .ToList();

        var completedContracts = matches
            .Where(m => m.Contract is not null && m.Contract.Status == ContractStatus.Signed)
            .ToList();

        var totalContracts = matches
            .Where(m => m.Contract is not null)
            .ToList();

        var profileCompletionPercent = CalculateCompletionPercent(farm);

        var certificationPercent = farm.FarmCertifications?.Count > 0
            ? Math.Min(farm.FarmCertifications.Count * 25m, 100)
            : 0m;

        var contractHistoryPercent = totalContracts.Count > 0
            ? Math.Round((decimal)completedContracts.Count / totalContracts.Count * 100, 1)
            : 0m;

        var buyerRatingPercent = farm.RatingCount > 0
            ? Math.Round(farm.AverageRating / 5m * 100, 1)
            : 0m;

        var improvementTips = new List<ImprovementTip>();

        var missingFields = new List<string>();
        if (string.IsNullOrWhiteSpace(farm.Name)) missingFields.Add("Farm name");
        if (string.IsNullOrWhiteSpace(farm.Location)) missingFields.Add("Location");
        if (string.IsNullOrWhiteSpace(farm.Governorate)) missingFields.Add("Governorate");
        if (farm.SoilType is null) missingFields.Add("Soil type");
        if (farm.SizeInFeddans is null || farm.SizeInFeddans <= 0) missingFields.Add("Farm size (feddan)");
        if (string.IsNullOrWhiteSpace(farm.User.PhoneNumber)) missingFields.Add("Phone number");
        if (farm.CropTypes.Count == 0) missingFields.Add("At least one crop type");
        if (farm.FarmDocuments.Count == 0) missingFields.Add("At least one document (e.g. agricultural deed)");

        if (profileCompletionPercent < 100)
        {
            var missing = string.Join(", ", missingFields);
            var msg = profileCompletionPercent < 50
                ? $"Complete your farm profile to boost this score. Missing: {missing}. Each of the 8 fields (name, location, governorate, soil type, size, phone, crop, document) adds 12.5% to this factor."
                : $"Almost complete! Finish these last items to reach 100%: {missing}. Each field adds 12.5% to this factor.";
            improvementTips.Add(new ImprovementTip
            {
                Category = "Profile Completion",
                CurrentScore = profileCompletionPercent,
                Severity = profileCompletionPercent < 50 ? "high" : profileCompletionPercent < 80 ? "medium" : "low",
                Message = msg,
                Icon = "person"
            });
        }

        if (certificationPercent < 100)
        {
            var count = farm.FarmCertifications?.Count ?? 0;
            var needed = count == 0 ? 4 : Math.Max(1, (int)Math.Ceiling((100 - certificationPercent) / 25m));
            var msg = certificationPercent == 0
                ? $"You have no certifications. Add GlobalGAP, Organic, Fair Trade, or other certs to your farm. Each certification adds 25 points to this factor — you need at least {needed} to max it out."
                : $"You have {count} certification(s) ({certificationPercent}%). Add {needed} more to reach 100%. Each certification adds 25 points to this factor.";
            improvementTips.Add(new ImprovementTip
            {
                Category = "Certifications",
                CurrentScore = certificationPercent,
                Severity = certificationPercent < 50 ? "high" : certificationPercent < 80 ? "medium" : "low",
                Message = msg,
                Icon = "verified"
            });
        }

        if (contractHistoryPercent < 100)
        {
            var msg = contractHistoryPercent == 0
                ? $"{completedContracts.Count} of {totalContracts.Count} contracts completed. Accept matches, fulfill deliveries on time, and get contracts marked complete. Every signed contract lifts this ratio."
                : $"{completedContracts.Count} of {totalContracts.Count} contracts completed ({contractHistoryPercent}%). To raise this, fulfill every accepted match and ensure contracts reach 'Signed' status.";
            improvementTips.Add(new ImprovementTip
            {
                Category = "Contract History",
                CurrentScore = contractHistoryPercent,
                Severity = contractHistoryPercent < 50 ? "high" : contractHistoryPercent < 80 ? "medium" : "low",
                Message = msg,
                Icon = "assignment_turned_in"
            });
        }

        if (buyerRatingPercent < 100)
        {
            var msg = buyerRatingPercent == 0
                ? $"No ratings yet ({farm.RatingCount} reviews). After a delivery, ask the factory buyer to rate your farm. Each 5-star review pulls your average toward 100%."
                : $"Your rating is {farm.AverageRating:F1}/5.0 across {farm.RatingCount} review(s). Focus on product quality, on-time delivery, and clear communication to earn higher scores from buyers.";
            improvementTips.Add(new ImprovementTip
            {
                Category = "Buyer Ratings",
                CurrentScore = buyerRatingPercent,
                Severity = buyerRatingPercent < 50 ? "high" : buyerRatingPercent < 80 ? "medium" : "low",
                Message = msg,
                Icon = "star"
            });
        }

        var recentMatches = matches
            .OrderByDescending(m => m.CreatedAt)
            .Take(5)
            .Select(m => new RecentMatchItem
            {
                MatchId = m.MatchId,
                FactoryName = m.SupplyRequest?.Factory?.Name ?? "Unknown",
                CropName = m.SupplyRequest?.CropType?.Name ?? "Unknown",
                QuantityTons = m.SupplyRequest?.QuantityTons ?? 0,
                MatchScore = m.MatchScore,
                Status = m.Status.ToString()
            })
            .ToList();

        var response = new FarmDashboardResponse
        {
            RiskScore = farm.RiskScore,
            ActiveMatchesCount = activeMatches.Count,
            CompletedContractsCount = completedContracts.Count,
            AverageRating = farm.AverageRating,
            RatingCount = farm.RatingCount,
            RiskBreakdown = new List<RiskBreakdownItem>
            {
                new() { Label = "Profile Completion", Percentage = profileCompletionPercent },
                new() { Label = "Certifications", Percentage = certificationPercent },
                new() { Label = "Contract History", Percentage = contractHistoryPercent },
                new() { Label = "Buyer Ratings", Percentage = buyerRatingPercent }
            },
            RecentMatches = recentMatches,
            ImprovementTips = improvementTips
        };

        return Result<FarmDashboardResponse>.Success(response);
    }

    public async Task<Guid> RegisterFarmAsync(Guid userId, string name, string governorate, decimal sizeInFeddans)
    {
        var farm = new Farm
        {
            FarmId = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Governorate = governorate,
            SizeInFeddans = sizeInFeddans
        };

        await _farmRepository.AddAsync(farm);
        await _unitOfWork.SaveChangesAsync();
        return farm.FarmId;
    }

    public async Task<Result<FarmProfileResponse>> GetProfileAsync(Guid userId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result<FarmProfileResponse>.Failure(FarmErrors.FarmNotFound);

        var response = MapToProfileResponse(farm);
        return Result<FarmProfileResponse>.Success(response);
    }

    public async Task<Result> UpdateProfileAsync(Guid userId, UpdateFarmProfileRequest request)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        farm.Name = request.Name;
        farm.Location = request.Location;
        farm.Governorate = request.Governorate;
        farm.SizeInFeddans = request.SizeInFeddans;
        farm.SoilType = request.SoilType;

        _farmRepository.Update(farm);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FarmDocumentDto>> AddDocumentAsync(Guid userId, IFormFile file)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<FarmDocumentDto>.Failure(FarmErrors.FarmNotFound);

        var (url, publicId) = await _cloudinaryService.UploadAsync(file);

        var document = new FarmDocument
        {
            FarmDocumentId = Guid.NewGuid(),
            FarmId = farm.FarmId,
            FileName = file.FileName,
            FileUrl = url,
            FileSize = file.Length,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(),
            PublicId = publicId
        };

        await _farmDocumentRepository.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();

        var dto = MapToDocumentDto(document);
        return Result<FarmDocumentDto>.Success(dto);
    }

    public async Task<Result> DeleteDocumentAsync(Guid userId, Guid documentId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var document = await _farmDocumentRepository.GetByIdAsync(documentId);
        if (document is null || document.FarmId != farm.FarmId)
            return Result.Failure(FarmErrors.DocumentNotFound);

        await _cloudinaryService.DeleteAsync(document.PublicId);
        _farmDocumentRepository.Remove(document);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> AddCropAsync(Guid userId, Guid cropTypeId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var cropType = await _cropTypeRepository.GetByIdAsync(cropTypeId);
        if (cropType is null)
            return Result.Failure(FarmErrors.CropTypeNotFound);

        if (farm.CropTypes.Any(c => c.CropTypeId == cropTypeId))
            return Result.Failure(FarmErrors.CropAlreadyAdded);

        farm.CropTypes.Add(cropType);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteCropAsync(Guid userId, Guid cropTypeId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var cropType = farm.CropTypes.FirstOrDefault(c => c.CropTypeId == cropTypeId);
        if (cropType is null)
            return Result.Failure(FarmErrors.CropTypeNotFound);

        farm.CropTypes.Remove(cropType);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<List<FarmMatchItemDto>>> GetMatchesAsync(Guid userId, string? status, Guid? cropTypeId)
    {
        var matches = await _farmRepository.GetFarmMatchesAsync(userId, status, cropTypeId);

        var dtos = matches.Select(m => new FarmMatchItemDto
        {
            MatchId = m.MatchId,
            FactoryName = m.SupplyRequest?.Factory?.Name ?? "Unknown",
            FactoryLocation = m.SupplyRequest?.Factory?.Location,
            FactoryIsVerified = m.SupplyRequest?.Factory?.IsVerified ?? false,
            CropName = m.SupplyRequest?.CropType?.Name ?? "Unknown",
            CropTypeId = m.SupplyRequest?.CropTypeId ?? Guid.Empty,
            QuantityTons = m.SupplyRequest?.QuantityTons ?? 0,
            PricePerTon = m.SupplyRequest?.PricePerTon,
            DeliveryDate = m.SupplyRequest?.DeliveryDate,
            QualitySpecs = m.SupplyRequest?.QualitySpecs,
            MatchScore = m.MatchScore,
            RiskScore = m.RiskScore,
            Status = m.Status.ToString(),
            CreatedAt = m.CreatedAt
        }).ToList();

        return Result<List<FarmMatchItemDto>>.Success(dtos);
    }

    public async Task<Result> RespondToMatchAsync(Guid userId, Guid matchId, string action)
    {
        var match = await _farmMatchRepository.GetByIdAsync(matchId);
        if (match is null)
            return Result.Failure(FarmErrors.MatchNotFound);

        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null || match.FarmId != farm.FarmId)
            return Result.Failure(FarmErrors.MatchNotFound);

        if (match.Status != FarmMatchStatus.Proposed)
            return Result.Failure(FarmErrors.MatchNotProposed);

        switch (action.ToLowerInvariant())
        {
            case "accept":
                match.Status = FarmMatchStatus.Accepted;
                break;
            case "reject":
                match.Status = FarmMatchStatus.Rejected;
                break;
            default:
                return Result.Failure(FarmErrors.InvalidAction);
        }

        _farmMatchRepository.Update(match);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    private FarmProfileResponse MapToProfileResponse(Farm farm)
    {
        return new FarmProfileResponse
        {
            FarmId = farm.FarmId,
            Name = farm.Name,
            Location = farm.Location,
            Governorate = farm.Governorate,
            SizeInFeddans = farm.SizeInFeddans,
            SoilType = farm.SoilType?.ToString(),
            Phone = farm.User.PhoneNumber,
            IsVerified = farm.IsVerified,
            CompletionPercent = CalculateCompletionPercent(farm),
            CropTypes = farm.CropTypes.Select(c => new CropTypeDto
            {
                CropTypeId = c.CropTypeId,
                Name = c.Name
            }).ToList(),
            Documents = farm.FarmDocuments.Select(MapToDocumentDto).ToList()
        };
    }

    private static FarmDocumentDto MapToDocumentDto(FarmDocument document)
    {
        return new FarmDocumentDto
        {
            DocumentId = document.FarmDocumentId,
            Name = document.FileName,
            FileUrl = document.FileUrl,
            Size = FormatFileSize(document.FileSize),
            FileType = document.FileType
        };
    }

    private static int CalculateCompletionPercent(Farm farm)
    {
        var fields = 0;

        if (!string.IsNullOrWhiteSpace(farm.Name)) fields++;
        if (!string.IsNullOrWhiteSpace(farm.Location)) fields++;
        if (!string.IsNullOrWhiteSpace(farm.Governorate)) fields++;
        if (farm.SoilType is not null) fields++;
        if (farm.SizeInFeddans is not null && farm.SizeInFeddans > 0) fields++;
        if (!string.IsNullOrWhiteSpace(farm.User.PhoneNumber)) fields++;
        if (farm.CropTypes.Count > 0) fields++;
        if (farm.FarmDocuments.Count > 0) fields++;

        return (int)Math.Round((fields / 8.0) * 100);
    }

    public async Task<Result<List<FarmContractDto>>> GetContractsAsync(Guid userId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<List<FarmContractDto>>.Failure(FarmErrors.FarmNotFound);

        var contracts = await _farmRepository.GetFarmContractsAsync(userId);

        var dtos = contracts.Select(c => new FarmContractDto
        {
            ContractId = c.ContractId,
            MatchId = c.MatchId,
            FactoryName = c.FarmMatch.SupplyRequest?.Factory?.Name ?? "Unknown",
            FactoryLocation = c.FarmMatch.SupplyRequest?.Factory?.Location,
            CropName = c.FarmMatch.SupplyRequest?.CropType?.Name ?? "Unknown",
            QuantityTons = c.FarmMatch.SupplyRequest?.QuantityTons ?? 0,
            PricePerTon = c.FarmMatch.SupplyRequest?.PricePerTon,
            DeliveryDate = c.FarmMatch.SupplyRequest?.DeliveryDate,
            Status = c.Status.ToString(),
            CreatedAt = c.CreatedAt,
            SignedAt = c.SignedAt
        }).ToList();

        return Result<List<FarmContractDto>>.Success(dtos);
    }

    public async Task<Result<List<ConversationDto>>> GetConversationsAsync(Guid userId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<List<ConversationDto>>.Failure(FarmErrors.FarmNotFound);

        var matches = await _farmRepository.GetConversationsAsync(userId);

        var dtos = matches.Select(m =>
        {
            var lastMsg = m.Messages.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
            return new ConversationDto
            {
                MatchId = m.MatchId,
                FactoryName = m.SupplyRequest?.Factory?.Name ?? "Unknown",
                CropName = m.SupplyRequest?.CropType?.Name,
                LastMessage = lastMsg?.Content,
                LastMessageAt = lastMsg?.CreatedAt,
                UnreadCount = m.Messages.Count(x => !x.IsRead && x.ReceiverId == farm.UserId)
            };
        }).OrderByDescending(d => d.LastMessageAt).ToList();

        return Result<List<ConversationDto>>.Success(dtos);
    }

    public async Task<Result<List<MessageDto>>> GetMessagesAsync(Guid userId, Guid matchId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<List<MessageDto>>.Failure(FarmErrors.FarmNotFound);

        var messages = await _farmRepository.GetMessagesAsync(userId, matchId);
        if (messages.Count == 0)
            return Result<List<MessageDto>>.Failure(FarmErrors.ConversationNotFound);

        var dtos = messages.Select(m => new MessageDto
        {
            MessageId = m.MessageId,
            MatchId = m.MatchId,
            SenderId = m.SenderId,
            SenderName = m.Sender.UserName ?? m.Sender.Email ?? "Unknown",
            Content = m.Content,
            IsRead = m.IsRead,
            CreatedAt = m.CreatedAt
        }).ToList();

        return Result<List<MessageDto>>.Success(dtos);
    }

    public async Task<Result> SendMessageAsync(Guid userId, Guid matchId, string content)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var matches = await _farmRepository.GetFarmMatchesAsync(userId, null, null);
        var match = matches.FirstOrDefault(m => m.MatchId == matchId);
        if (match is null)
            return Result.Failure(FarmErrors.ConversationNotFound);

        if (string.IsNullOrWhiteSpace(content))
            return Result.Failure(FarmErrors.InvalidAction);

        var receiverId = match.SupplyRequest?.Factory?.UserId ?? Guid.Empty;

        var message = new Message
        {
            MessageId = Guid.NewGuid(),
            MatchId = matchId,
            SenderId = userId,
            ReceiverId = receiverId,
            Content = content,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        await _messageRepository.AddAsync(message);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<List<FarmNotificationDto>>> GetNotificationsAsync(Guid userId)
    {
        var notifications = await _farmRepository.GetNotificationsAsync(userId);

        var dtos = notifications.Select(n => new FarmNotificationDto
        {
            NotificationId = n.NotificationId,
            Title = n.Title,
            Message = n.Message,
            Type = n.Type,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();

        return Result<List<FarmNotificationDto>>.Success(dtos);
    }

    public async Task<Result> MarkNotificationAsReadAsync(Guid userId, Guid notificationId)
    {
        var notifications = await _farmRepository.GetNotificationsAsync(userId);
        var notification = notifications.FirstOrDefault(n => n.NotificationId == notificationId);

        if (notification is null)
            return Result.Failure(FarmErrors.NotificationNotFound);

        notification.IsRead = true;
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
