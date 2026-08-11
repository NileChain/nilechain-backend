using NileChain.Application.Common;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Validation;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Application.Services;

public class FarmService : IFarmService
{
    private readonly IFarmRepository _farmRepository;
    private readonly IRepository<CropType> _cropTypeRepository;
    private readonly IRepository<FarmDocument> _farmDocumentRepository;
    private readonly IRepository<FarmMatch> _farmMatchRepository;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<Contract> _contractRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IContractPdfService _pdfService;
    private readonly IFulfillmentService _fulfillmentService;
    private readonly IPaymentMilestoneService _paymentMilestoneService;
    private readonly IUnitOfWork _unitOfWork;

    public FarmService(
        IFarmRepository farmRepository,
        IRepository<CropType> cropTypeRepository,
        IRepository<FarmDocument> farmDocumentRepository,
        IRepository<FarmMatch> farmMatchRepository,
        IRepository<Message> messageRepository,
        IRepository<Contract> contractRepository,
        IRepository<Notification> notificationRepository,
        ICloudinaryService cloudinaryService,
        IContractPdfService pdfService,
        IFulfillmentService fulfillmentService,
        IPaymentMilestoneService paymentMilestoneService,
        IUnitOfWork unitOfWork)
    {
        _farmRepository = farmRepository;
        _cropTypeRepository = cropTypeRepository;
        _farmDocumentRepository = farmDocumentRepository;
        _farmMatchRepository = farmMatchRepository;
        _messageRepository = messageRepository;
        _contractRepository = contractRepository;
        _notificationRepository = notificationRepository;
        _cloudinaryService = cloudinaryService;
        _pdfService = pdfService;
        _fulfillmentService = fulfillmentService;
        _paymentMilestoneService = paymentMilestoneService;
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

        var reliabilityTrend = BuildReliabilityTrend(matches, farm.RiskScore);

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
            ImprovementTips = improvementTips,
            ReliabilityTrend = reliabilityTrend
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
        farm.Latitude = request.Latitude;
        farm.Longitude = request.Longitude;
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

        if (file is null || file.Length <= 0)
            return Result<FarmDocumentDto>.Failure(new Error("File.Empty", "File is required."));

        await using var probe = file.OpenReadStream();
        var validation = FileUploadValidation.Validate(
            file.FileName,
            file.ContentType,
            file.Length,
            probe);

        if (!validation.IsValid)
        {
            return Result<FarmDocumentDto>.Failure(new Error(
                validation.ErrorCode ?? "Farm.DocumentInvalid",
                validation.ErrorMessage ?? FarmErrors.DocumentInvalid.Description));
        }

        var (url, publicId) = await _cloudinaryService.UploadAsync(file);

        var document = new FarmDocument
        {
            FarmDocumentId = Guid.NewGuid(),
            FarmId = farm.FarmId,
            FileName = Path.GetFileName(file.FileName),
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

    public async Task<Result<List<FarmDocumentDto>>> GetDocumentsAsync(Guid userId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result<List<FarmDocumentDto>>.Failure(FarmErrors.FarmNotFound);

        var dtos = farm.FarmDocuments.Select(MapToDocumentDto).ToList();
        return Result<List<FarmDocumentDto>>.Success(dtos);
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

    public async Task<Result<FarmMatchesListResponse>> GetMatchesAsync(
        Guid userId,
        string? status,
        Guid? cropTypeId,
        string? sort = null,
        string? search = null,
        int? days = null,
        int page = 1,
        int pageSize = 20)
    {
        // Ordering: MatchListOrdering — default CreatedAt DESC (newest first). Pagination after sort.
        var newSince = DateTime.UtcNow.AddHours(-72);
        var (items, total) = await _farmRepository.GetFarmMatchesPageAsync(
            userId,
            status,
            cropTypeId,
            sort,
            search,
            days,
            page,
            pageSize);

        var counts = await _farmRepository.GetFarmMatchCountsAsync(userId, newSince);
        var newMatches = await _farmRepository.GetNewFarmMatchesAsync(userId, newSince, 5);

        return Result<FarmMatchesListResponse>.Success(new FarmMatchesListResponse
        {
            Items = items.Select(MapMatchItem).ToList(),
            TotalCount = total,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100),
            Summary = new FarmMatchSummaryDto
            {
                Total = counts.Total,
                Proposed = counts.Proposed,
                Accepted = counts.Accepted,
                Rejected = counts.Rejected,
                NewCount = counts.NewCount
            },
            NewMatches = newMatches.Select(MapMatchItem).ToList()
        });
    }

    private static FarmMatchItemDto MapMatchItem(Domain.Entities.FarmMatch m) => new()
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
        CreatedAt = m.CreatedAt,
        ContractId = m.Contract?.ContractId
    };

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

        // Accepting a match is only allowed from the Contract Details page.
        if (action.Equals("accept", StringComparison.OrdinalIgnoreCase))
            return Result.Failure(new Error(
                "Farm.AcceptViaContractOnly",
                "Review and accept the contract from the Contract Details page."));

        switch (action.ToLowerInvariant())
        {
            case "reject":
                match.Status = FarmMatchStatus.Rejected;
                break;
            default:
                return Result.Failure(FarmErrors.InvalidAction);
        }

        _farmMatchRepository.Update(match);

        var existing = await _farmRepository.GetContractByMatchForFarmAsync(userId, matchId);
        if (existing is not null &&
            existing.Status is ContractStatus.PendingSignature
                or ContractStatus.Draft
                or ContractStatus.PendingFarmSignature
                or ContractStatus.PendingFactorySignature)
        {
            existing.Status = ContractStatus.Cancelled;
            existing.ClearSignatures();
            _contractRepository.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FarmContractDto>> GetOrCreateContractForMatchAsync(Guid userId, Guid matchId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<FarmContractDto>.Failure(FarmErrors.FarmNotFound);

        var existing = await _farmRepository.GetContractByMatchForFarmAsync(userId, matchId);
        if (existing is not null)
            return Result<FarmContractDto>.Success(MapContract(existing));

        var match = await _farmRepository.GetFarmMatchByIdAsync(userId, matchId);
        if (match is null)
            return Result<FarmContractDto>.Failure(FarmErrors.MatchNotFound);

        if (!IsPartyActive(match.Farm?.User) || !IsPartyActive(match.SupplyRequest?.Factory?.User))
            return Result<FarmContractDto>.Failure(FarmErrors.PartyInactive);

        if (!MatchGovernoratePolicy.IsSnapshotStillValid(
                match.MatchedGovernorate,
                match.Farm?.Governorate,
                match.SupplyRequest?.QualitySpecs))
        {
            return Result<FarmContractDto>.Failure(FarmErrors.GovernorateMismatch);
        }

        if (!ContractExecution.CanCreateContract(match))
            return Result<FarmContractDto>.Failure(FarmErrors.MatchNotProposed);

        var factoryName = match.SupplyRequest?.Factory?.Name ?? "Factory";
        var farmName = match.Farm?.Name ?? farm.Name;
        var crop = match.SupplyRequest?.CropType?.Name ?? "Crop";
        var qty = match.SupplyRequest?.QuantityTons ?? 0;
        var price = match.SupplyRequest?.PricePerTon;
        var delivery = match.SupplyRequest?.DeliveryDate;
        var location = match.SupplyRequest?.Factory?.Location
                       ?? match.SupplyRequest?.Factory?.Governorate
                       ?? "—";
        var quality = match.SupplyRequest?.QualitySpecs;

        var text = BuildReviewContractText(
            factoryName,
            farmName,
            crop,
            qty,
            price,
            delivery,
            location,
            quality);

        var contract = new Contract
        {
            ContractId = Guid.NewGuid(),
            MatchId = match.MatchId,
            GeneratedText = text,
            Status = ContractStatus.PendingSignature,
            CreatedAt = DateTime.UtcNow
        };

        await _contractRepository.AddAsync(contract);
        await _unitOfWork.SaveChangesAsync();

        var created = await _farmRepository.GetContractForFarmAsync(userId, contract.ContractId);
        return Result<FarmContractDto>.Success(MapContract(created ?? contract));
    }

    private static string BuildReviewContractText(
        string factoryName,
        string farmName,
        string crop,
        decimal qty,
        decimal? price,
        DateTime? delivery,
        string location,
        string? quality)
    {
        var priceLine = price is null
            ? "Price per ton to be confirmed in writing."
            : $"Price: {price:N0} EGP per ton.";
        var deliveryLine = delivery is null
            ? "Delivery date to be agreed by both parties."
            : $"Delivery Date: {delivery:yyyy-MM-dd}.";
        var qualityLine = string.IsNullOrWhiteSpace(quality)
            ? "Goods must meet customary market quality standards for the crop."
            : $"Quality Specifications: {quality}";

        return
            $"""
            Agricultural Supply Contract

            1. Parties
            This agreement is entered into between {factoryName} (the "Factory" / Buyer) and {farmName} (the "Farm" / Supplier).

            2. Scope
            The Farm agrees to supply {qty:N2} tons of {crop} to the Factory under the terms of this contract.

            3. Payment
            {priceLine}
            Payment shall be settled after delivery and inspection confirmation by the Factory.
            Late payment may incur standard NileChain settlement remedies.

            4. Delivery
            {deliveryLine}
            Delivery Location: {location}.
            Risk of loss transfers upon accepted delivery at the stated location.

            5. Responsibilities
            The Farm shall ensure timely harvest, packaging, and dispatch.
            The Factory shall provide receiving capacity and complete inspection within a reasonable period.

            6. Quality Standards
            {qualityLine}
            Non-conforming shipments may be rejected or subject to price adjustment.

            7. Force Majeure
            Neither party is liable for delays caused by events beyond reasonable control, including extreme weather, provided prompt notice is given.

            8. Termination
            Either party may terminate for material breach if not cured within a reasonable cure period after written notice.
            Unilateral rejection before signature cancels this draft without liability beyond reasonable reliance costs.

            9. Signatures
            By accepting this contract in NileChain, each party confirms they have reviewed all terms and agree to be bound.
            """;
    }

    private FarmProfileResponse MapToProfileResponse(Farm farm)
    {
        return new FarmProfileResponse
        {
            FarmId = farm.FarmId,
            Name = farm.Name,
            Location = farm.Location,
            Governorate = farm.Governorate,
            Latitude = farm.Latitude,
            Longitude = farm.Longitude,
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

    private static List<ReliabilityTrendPoint> BuildReliabilityTrend(
        IEnumerable<FarmMatch> matches,
        decimal? currentRiskScore)
    {
        var points = matches
            .Where(m => m.RiskScore.HasValue)
            .OrderBy(m => m.CreatedAt)
            .Select(m => new ReliabilityTrendPoint
            {
                Value = Math.Clamp(Math.Round(m.RiskScore!.Value, 0), 0, 100),
                Label = m.CreatedAt.ToString("dd MMM")
            })
            .ToList();

        if (currentRiskScore.HasValue)
        {
            var current = Math.Clamp(Math.Round(currentRiskScore.Value, 0), 0, 100);
            if (points.Count == 0 || points[^1].Value != current)
            {
                points.Add(new ReliabilityTrendPoint
                {
                    Value = current,
                    Label = DateTime.UtcNow.ToString("dd MMM")
                });
            }
        }

        if (points.Count > 12)
            points = points.TakeLast(12).ToList();

        return points;
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
        return Result<List<FarmContractDto>>.Success(contracts.Select(MapContract).ToList());
    }

    public async Task<Result<FarmContractDto>> GetContractAsync(Guid userId, Guid contractId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<FarmContractDto>.Failure(FarmErrors.FarmNotFound);

        var contract = await _farmRepository.GetContractForFarmAsync(userId, contractId);
        if (contract is null)
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotFound);

        return Result<FarmContractDto>.Success(MapContract(contract));
    }

    public async Task<Result<FarmContractDto>> ApproveContractAsync(Guid userId, Guid contractId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<FarmContractDto>.Failure(FarmErrors.FarmNotFound);

        var contract = await _farmRepository.GetContractForFarmAsync(userId, contractId);
        if (contract is null)
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotFound);

        if (contract.Status == ContractStatus.Cancelled)
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotPending);

        var match = contract.FarmMatch;
        if (!IsPartyActive(match?.Farm?.User) || !IsPartyActive(match?.SupplyRequest?.Factory?.User))
            return Result<FarmContractDto>.Failure(FarmErrors.PartyInactive);

        if (!MatchGovernoratePolicy.IsSnapshotStillValid(
                match?.MatchedGovernorate,
                match?.Farm?.Governorate,
                match?.SupplyRequest?.QualitySpecs))
        {
            return Result<FarmContractDto>.Failure(FarmErrors.GovernorateMismatch);
        }

        if (!ContractExecution.CanSign(match))
            return Result<FarmContractDto>.Failure(FarmErrors.MatchNotProposed);

        // Idempotent: farm already signed — do not touch factory signature.
        if (contract.IsFarmSigned)
            return Result<FarmContractDto>.Success(MapContract(contract));

        if (contract.Status is not (
                ContractStatus.Draft
                or ContractStatus.PendingSignature
                or ContractStatus.PendingFarmSignature))
        {
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotPending);
        }

        contract.FarmSignedAt = DateTime.UtcNow;
        // FactorySignedAt must remain unchanged.
        contract.RefreshSignatureStatus();
        ContractExecution.AcceptMatchIfFullySigned(contract);
        _contractRepository.Update(contract);

        var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
        if (factoryUserId is Guid uid && uid != Guid.Empty)
        {
            var fullySigned = contract.IsFullySigned;
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = fullySigned ? "Contract fully signed" : "Contract signed by farm",
                Message = fullySigned
                    ? $"{farm.Name} signed the supply contract. Both parties have now signed."
                    : $"{farm.Name} signed the supply contract. Factory signature is still required.",
                Type = "Contract",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        var approveSaved = await TrySaveContractChangesAsync();
        if (approveSaved.IsFailure)
            return Result<FarmContractDto>.Failure(approveSaved.Error!);

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

        return Result<FarmContractDto>.Success(MapContract(contract));
    }

    public async Task<Result<FarmContractDto>> RejectContractAsync(Guid userId, Guid contractId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<FarmContractDto>.Failure(FarmErrors.FarmNotFound);

        var contract = await _farmRepository.GetContractForFarmAsync(userId, contractId);
        if (contract is null)
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotFound);

        if (contract.Status is ContractStatus.Signed or ContractStatus.Cancelled)
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotPending);

        if (contract.Status is not (
                ContractStatus.Draft
                or ContractStatus.PendingSignature
                or ContractStatus.PendingFarmSignature
                or ContractStatus.PendingFactorySignature))
        {
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotPending);
        }

        contract.Status = ContractStatus.Cancelled;
        contract.ClearSignatures();
        ContractExecution.RejectMatchIfProposed(contract);
        _contractRepository.Update(contract);

        var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
        if (factoryUserId is Guid uid && uid != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = "Contract rejected by farm",
                Message = $"{farm.Name} rejected the supply contract.",
                Type = "Contract",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        var rejectSaved = await TrySaveContractChangesAsync();
        if (rejectSaved.IsFailure)
            return Result<FarmContractDto>.Failure(rejectSaved.Error!);

        await _fulfillmentService.VoidForContractAsync(
            contract.ContractId,
            userId,
            "Contract cancelled by farm");
        await _paymentMilestoneService.VoidForContractAsync(
            contract.ContractId,
            userId,
            "Contract cancelled by farm — payment milestone schedule voided");

        return Result<FarmContractDto>.Success(MapContract(contract));
    }

    public async Task<Result<(byte[] PdfBytes, string FileName)>> GetContractPdfAsync(
        Guid userId,
        Guid contractId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<(byte[], string)>.Failure(FarmErrors.FarmNotFound);

        var contract = await _farmRepository.GetContractForFarmAsync(userId, contractId);
        if (contract is null)
            return Result<(byte[], string)>.Failure(FarmErrors.ContractNotFound);

        var text = contract.GeneratedText ?? string.Empty;
        var farmName = contract.FarmMatch?.Farm?.Name ?? farm.Name;
        var factoryName = contract.FarmMatch?.SupplyRequest?.Factory?.Name ?? "Factory";
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

    private static FarmContractDto MapContract(Contract c)
    {
        var factory = c.FarmMatch?.SupplyRequest?.Factory;
        var farm = c.FarmMatch?.Farm;
        var supply = c.FarmMatch?.SupplyRequest;
        return new FarmContractDto
        {
            ContractId = c.ContractId,
            MatchId = c.MatchId,
            FactoryName = factory?.Name ?? "Unknown",
            FactoryLocation = factory?.Location ?? factory?.Governorate,
            FarmName = farm?.Name ?? "Unknown",
            CropName = supply?.CropType?.Name ?? "Unknown",
            QuantityTons = supply?.QuantityTons ?? 0,
            PricePerTon = supply?.PricePerTon,
            DeliveryDate = supply?.DeliveryDate,
            DeliveryLocation = factory?.Location ?? factory?.Governorate,
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
    }

    private static bool IsPartyActive(NileChain.Domain.Identity.ApplicationUser? user) =>
        user is null || user.IsActive;

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

    private async Task<Result> TrySaveContractChangesAsync()
    {
        try
        {
            await _unitOfWork.SaveChangesAsync();
            return Result.Success();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Result.Failure(FarmErrors.ConcurrencyConflict);
        }
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
