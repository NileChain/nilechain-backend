using NileChain.Application.Common;
using NileChain.Application.Contracts;
using NileChain.Application.Dtos.Contracts;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Matching;
using NileChain.Application.Validation;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace NileChain.Application.Services;

public class FarmService : IFarmService
{
    private readonly IFarmRepository _farmRepository;
    private readonly IRepository<CropType> _cropTypeRepository;
    private readonly IRepository<FarmDocument> _farmDocumentRepository;
    private readonly IRepository<Certification> _certificationRepository;
    private readonly IRepository<FarmMatch> _farmMatchRepository;
    private readonly IRepository<Message> _messageRepository;
    private readonly IRepository<Contract> _contractRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IContractPdfService _pdfService;
    private readonly IFulfillmentService _fulfillmentService;
    private readonly IPaymentMilestoneService _paymentMilestoneService;
    private readonly IWalletService _walletService;
    private readonly IMockEscrowPaymentService _escrowPayments;
    private readonly IContractIntegrityService _integrity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISigningOtpService _signingOtp;
    private readonly IContractHashService _contractHash;
    private readonly IContractSignatureRepository _signatures;
    private readonly IEmailService _email;
    private readonly ILogger<FarmService> _logger;
    private readonly UserManager<ApplicationUser>? _userManager;
    private readonly ISubscriptionService? _subscriptions;

    public FarmService(
        IFarmRepository farmRepository,
        IRepository<CropType> cropTypeRepository,
        IRepository<FarmDocument> farmDocumentRepository,
        IRepository<Certification> certificationRepository,
        IRepository<FarmMatch> farmMatchRepository,
        IRepository<Message> messageRepository,
        IRepository<Contract> contractRepository,
        IRepository<Notification> notificationRepository,
        ICloudinaryService cloudinaryService,
        IContractPdfService pdfService,
        IFulfillmentService fulfillmentService,
        IPaymentMilestoneService paymentMilestoneService,
        IWalletService walletService,
        IMockEscrowPaymentService escrowPayments,
        IContractIntegrityService integrity,
        IUnitOfWork unitOfWork,
        ISigningOtpService? signingOtp = null,
        IContractHashService? contractHash = null,
        IContractSignatureRepository? signatures = null,
        IEmailService? email = null,
        ILogger<FarmService>? logger = null,
        UserManager<ApplicationUser>? userManager = null,
        ISubscriptionService? subscriptions = null)
    {
        _farmRepository = farmRepository;
        _cropTypeRepository = cropTypeRepository;
        _farmDocumentRepository = farmDocumentRepository;
        _certificationRepository = certificationRepository;
        _farmMatchRepository = farmMatchRepository;
        _messageRepository = messageRepository;
        _contractRepository = contractRepository;
        _notificationRepository = notificationRepository;
        _cloudinaryService = cloudinaryService;
        _pdfService = pdfService;
        _fulfillmentService = fulfillmentService;
        _paymentMilestoneService = paymentMilestoneService;
        _walletService = walletService;
        _escrowPayments = escrowPayments;
        _integrity = integrity;
        _unitOfWork = unitOfWork;
        _signingOtp = signingOtp!;
        _contractHash = contractHash!;
        _signatures = signatures!;
        _email = email!;
        _logger = logger!;
        _userManager = userManager;
        _subscriptions = subscriptions;
    }

    public async Task<Result<FarmDashboardResponse>> GetDashboardAsync(Guid userId)
    {
        var farm = await _farmRepository.GetFarmWithDashboardDataAsync(userId);
        if (farm is null)
            return Result<FarmDashboardResponse>.Failure(FarmErrors.FarmNotFound);

        var matches = farm.FarmMatches ?? new List<FarmMatch>();
        var activeMatches = matches
            .Where(m => m.Status is FarmMatchStatus.Proposed
                or FarmMatchStatus.Countered
                or FarmMatchStatus.Accepted)
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
        if (farm.FarmCrops.Count == 0) missingFields.Add("At least one crop type");
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
        var collectionsSummary = BuildCollectionsSummary(matches);
        var now = DateTime.UtcNow;
        var expiringCerts = farm.FarmCertifications?
            .Count(c => c.ExpiresAt is not null
                && c.ExpiresAt > now
                && c.ExpiresAt <= now.AddDays(30)) ?? 0;
        var expiredCerts = farm.FarmCertifications?
            .Count(c => c.ExpiresAt is not null && c.ExpiresAt <= now) ?? 0;

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
            ReliabilityTrend = reliabilityTrend,
            CollectionsSummary = collectionsSummary,
            ExpiringCertifications = expiringCerts,
            ExpiredCertifications = expiredCerts,
            OnTimeFulfillmentRate = BuildOnTimeRate(completedContracts),
            QcIssueRate = BuildQcIssueRate(completedContracts),
            RepeatBuyers = BuildRepeatBuyers(completedContracts)
        };

        return Result<FarmDashboardResponse>.Success(response);
    }

    private static decimal? BuildOnTimeRate(IReadOnlyList<FarmMatch> signedMatches)
    {
        var scored = 0;
        var onTime = 0;
        foreach (var m in signedMatches)
        {
            var fulfillment = m.Contract?.Fulfillment;
            var delivery = m.SupplyRequest?.DeliveryDate;
            if (fulfillment is null
                || fulfillment.Status != FulfillmentStatus.Fulfilled
                || fulfillment.ReceivedAt is null
                || delivery is null)
                continue;

            scored++;
            var deadline = delivery.Value.Date.AddDays(3);
            if (fulfillment.ReceivedAt.Value.Date <= deadline)
                onTime++;
        }

        if (scored == 0)
            return null;
        return Math.Round((decimal)onTime / scored * 100m, 1);
    }

    private static decimal? BuildQcIssueRate(IReadOnlyList<FarmMatch> signedMatches)
    {
        var withQc = 0;
        var issues = 0;
        foreach (var m in signedMatches)
        {
            var f = m.Contract?.Fulfillment;
            if (f is null || f.QualityCheckedAt is null)
                continue;
            withQc++;
            var requested = m.SupplyRequest?.QuantityTons;
            if (f.DiscountPercent > 0
                || (f.AcceptedQuantityTons is not null
                    && requested is not null
                    && f.AcceptedQuantityTons < requested))
                issues++;
        }

        if (withQc == 0)
            return null;
        return Math.Round((decimal)issues / withQc * 100m, 1);
    }

    private static List<RepeatBuyerDto> BuildRepeatBuyers(IReadOnlyList<FarmMatch> signedMatches)
    {
        return signedMatches
            .Where(m => m.SupplyRequest?.Factory is not null)
            .GroupBy(m => m.SupplyRequest!.FactoryId)
            .Select(g => new RepeatBuyerDto
            {
                FactoryId = g.Key,
                FactoryName = g.First().SupplyRequest!.Factory!.Name,
                CompletedContracts = g.Count()
            })
            .OrderByDescending(x => x.CompletedContracts)
            .ThenBy(x => x.FactoryName)
            .Take(8)
            .ToList();
    }

    private static FarmCollectionsSummaryDto BuildCollectionsSummary(IEnumerable<FarmMatch> matches)
    {
        var today = DateTime.UtcNow.Date;
        var transactions = matches
            .Select(m => m.Contract)
            .Where(c => c is not null && c.Status == ContractStatus.Signed)
            .SelectMany(c => c!.Transactions ?? Enumerable.Empty<Transaction>())
            .Where(t => t.Status != TransactionStatus.Voided)
            .ToList();

        return new FarmCollectionsSummaryDto
        {
            PendingAmount = transactions
                .Where(t => t.Status == TransactionStatus.Pending)
                .Sum(t => t.Amount),
            AwaitingConfirmAmount = transactions
                .Where(t => t.Status == TransactionStatus.MarkedPaid)
                .Sum(t => t.Amount),
            ReceivedAmount = transactions
                .Where(t => t.Status == TransactionStatus.Completed)
                .Sum(t => t.Amount),
            OverdueAmount = transactions
                .Where(t =>
                    (t.Status == TransactionStatus.Pending || t.Status == TransactionStatus.MarkedPaid)
                    && t.DueDate is not null
                    && t.DueDate.Value.Date < today)
                .Sum(t => t.Amount),
            Currency = "EGP"
        };
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
        farm.Description = string.IsNullOrWhiteSpace(request.Description)
            ? null
            : request.Description.Trim();
        farm.BankName = string.IsNullOrWhiteSpace(request.BankName) ? null : request.BankName.Trim();
        farm.AccountHolderName = string.IsNullOrWhiteSpace(request.AccountHolderName)
            ? null
            : request.AccountHolderName.Trim();
        farm.BankAccountNumber = string.IsNullOrWhiteSpace(request.BankAccountNumber)
            ? null
            : request.BankAccountNumber.Trim();
        farm.Iban = string.IsNullOrWhiteSpace(request.Iban) ? null : request.Iban.Trim().ToUpperInvariant();

        _farmRepository.Update(farm);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FarmDocumentDto>> AddDocumentAsync(
        Guid userId,
        IFormFile file,
        string? kybKind)
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

        // kybKind comes from the frontend dropdown (enum value as string).
        // If parsing fails or is missing, default to Other.
        var parsedKybKind = Enum.TryParse<KybKind>(
            kybKind,
            ignoreCase: true,
            out var k)
            ? k
            : KybKind.Other;

        var document = new FarmDocument
        {
            FarmDocumentId = Guid.NewGuid(),
            FarmId = farm.FarmId,
            FileName = Path.GetFileName(file.FileName),
            FileUrl = url,
            FileSize = file.Length,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(),
            PublicId = publicId,
            KybKind = parsedKybKind
        };

        await _farmDocumentRepository.AddAsync(document);
        await RequeueKybIfNeededAsync(userId);
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

    public async Task<Result> AddCropAsync(Guid userId, AddCropRequest request)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        if (!IsValidCropCommercialTerms(
                request.AvailableQuantityTons,
                request.AvailableFrom,
                request.AvailableTo,
                request.MinPricePerTon))
        {
            return Result.Failure(FarmErrors.InvalidCropAvailability);
        }

        var cropType = await _cropTypeRepository.GetByIdAsync(request.CropTypeId);
        if (cropType is null)
            return Result.Failure(FarmErrors.CropTypeNotFound);

        if (farm.FarmCrops.Any(c => c.CropTypeId == request.CropTypeId))
            return Result.Failure(FarmErrors.CropAlreadyAdded);

        farm.FarmCrops.Add(new FarmCrop
        {
            FarmId = farm.FarmId,
            CropTypeId = request.CropTypeId,
            AvailableQuantityTons = request.AvailableQuantityTons,
            AvailableFrom = NormalizeDate(request.AvailableFrom),
            AvailableTo = NormalizeDate(request.AvailableTo),
            MinPricePerTon = request.MinPricePerTon,
            IsPublished = true,
            CropType = cropType
        });
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> UpdateCropAsync(Guid userId, Guid cropTypeId, UpdateFarmCropRequest request)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        if (!IsValidCropCommercialTerms(
                request.AvailableQuantityTons,
                request.AvailableFrom,
                request.AvailableTo,
                request.MinPricePerTon))
        {
            return Result.Failure(FarmErrors.InvalidCropAvailability);
        }

        var farmCrop = farm.FarmCrops.FirstOrDefault(c => c.CropTypeId == cropTypeId);
        if (farmCrop is null)
            return Result.Failure(FarmErrors.CropNotOnFarm);

        farmCrop.AvailableQuantityTons = request.AvailableQuantityTons;
        farmCrop.AvailableFrom = NormalizeDate(request.AvailableFrom);
        farmCrop.AvailableTo = NormalizeDate(request.AvailableTo);
        farmCrop.MinPricePerTon = request.MinPricePerTon;
        if (request.IsPublished is not null)
            farmCrop.IsPublished = request.IsPublished.Value;
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteCropAsync(Guid userId, Guid cropTypeId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var farmCrop = farm.FarmCrops.FirstOrDefault(c => c.CropTypeId == cropTypeId);
        if (farmCrop is null)
            return Result.Failure(FarmErrors.CropTypeNotFound);

        farm.FarmCrops.Remove(farmCrop);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FarmImageDto>> AddImageAsync(Guid userId, IFormFile file)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result<FarmImageDto>.Failure(FarmErrors.FarmNotFound);

        if (file is null || file.Length <= 0)
            return Result<FarmImageDto>.Failure(new Error("File.Empty", "File is required."));

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp"))
            return Result<FarmImageDto>.Failure(FarmErrors.ImageInvalid);

        await using var probe = file.OpenReadStream();
        var validation = FileUploadValidation.Validate(
            file.FileName,
            file.ContentType,
            file.Length,
            probe);
        if (!validation.IsValid)
        {
            return Result<FarmImageDto>.Failure(new Error(
                validation.ErrorCode ?? "Farm.ImageInvalid",
                validation.ErrorMessage ?? FarmErrors.ImageInvalid.Description));
        }

        var (url, publicId) = await _cloudinaryService.UploadAsync(file);
        var sortOrder = farm.FarmImages.Count == 0
            ? 0
            : farm.FarmImages.Max(i => i.SortOrder) + 1;

        var image = new FarmImage
        {
            FarmImageId = Guid.NewGuid(),
            FarmId = farm.FarmId,
            FileName = Path.GetFileName(file.FileName),
            FileUrl = url,
            PublicId = publicId,
            SortOrder = sortOrder,
            UploadedAt = DateTime.UtcNow
        };

        farm.FarmImages.Add(image);
        await _unitOfWork.SaveChangesAsync();

        return Result<FarmImageDto>.Success(new FarmImageDto
        {
            ImageId = image.FarmImageId,
            FileName = image.FileName,
            FileUrl = image.FileUrl,
            SortOrder = image.SortOrder
        });
    }

    public async Task<Result> DeleteImageAsync(Guid userId, Guid imageId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var image = farm.FarmImages.FirstOrDefault(i => i.FarmImageId == imageId);
        if (image is null)
            return Result.Failure(FarmErrors.ImageNotFound);

        await _cloudinaryService.DeleteAsync(image.PublicId);
        farm.FarmImages.Remove(image);
        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> CounterOfferAsync(Guid userId, Guid matchId, CounterOfferRequest request)
    {
        var match = await _farmRepository.GetFarmMatchByIdAsync(userId, matchId);
        if (match is null)
            return Result.Failure(FarmErrors.MatchNotFound);

        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null || match.FarmId != farm.FarmId)
            return Result.Failure(FarmErrors.MatchNotFound);

        if (match.Status is not (FarmMatchStatus.Proposed or FarmMatchStatus.Countered))
            return Result.Failure(FarmErrors.MatchNotCounterable);

        if (request.QuantityTons is null
            && request.PricePerTon is null
            && request.DeliveryDate is null)
        {
            return Result.Failure(FarmErrors.InvalidCounterOffer);
        }

        if (request.QuantityTons is <= 0 || request.PricePerTon is < 0)
            return Result.Failure(FarmErrors.InvalidCounterOffer);

        if (!MatchMessaging.CanAddRound(match))
            return Result.Failure(FarmErrors.NegotiationRoundLimit);

        var spendAccept = ShouldSpendFarmAccept(match);
        if (spendAccept && _subscriptions is not null)
        {
            var quota = await _subscriptions.EnsureCanConsumeAsync(userId, SubscriptionMetric.FarmAccepts);
            if (quota.IsFailure)
                return quota;
        }

        MatchNegotiationWriter.Append(
            match,
            DealParty.Farm,
            request.QuantityTons,
            request.PricePerTon,
            NormalizeDate(request.DeliveryDate),
            request.Note,
            request.Grade);
        _farmMatchRepository.Update(match);

        var detailed = await _farmRepository.GetFarmMatchByIdAsync(userId, matchId);
        var factoryUserId = detailed?.SupplyRequest?.Factory?.UserId;
        if (factoryUserId is Guid uid && uid != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = "Farm counter-offer received",
                Message = $"{farm.Name} proposed alternate terms on a match.",
                Type = "Match",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
        if (spendAccept && _subscriptions is not null)
            await _subscriptions.ConsumeAsync(userId, SubscriptionMetric.FarmAccepts);
        return Result.Success();
    }

    public async Task<Result> AcceptCounterOfferAsync(Guid userId, Guid matchId)
    {
        var match = await _farmRepository.GetFarmMatchByIdAsync(userId, matchId);
        if (match is null)
            return Result.Failure(FarmErrors.MatchNotFound);

        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null || match.FarmId != farm.FarmId)
            return Result.Failure(FarmErrors.MatchNotFound);

        if (!MatchMessaging.CanAcceptCounter(match, DealParty.Farm))
            return Result.Failure(FarmErrors.MatchNotCountered);

        var spendAccept = ShouldSpendFarmAccept(match);
        if (spendAccept && _subscriptions is not null)
        {
            var quota = await _subscriptions.EnsureCanConsumeAsync(userId, SubscriptionMetric.FarmAccepts);
            if (quota.IsFailure)
                return quota;
        }

        match.CounterAccepted = true;
        match.Status = FarmMatchStatus.Proposed;
        _farmMatchRepository.Update(match);

        var factoryUserId = match.SupplyRequest?.Factory?.UserId;
        if (factoryUserId is Guid uid && uid != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = "Factory counter-offer accepted",
                Message = $"{farm.Name} accepted your counter terms. You can proceed to the contract.",
                Type = "Match",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
        if (spendAccept && _subscriptions is not null)
            await _subscriptions.ConsumeAsync(userId, SubscriptionMetric.FarmAccepts);
        return Result.Success();
    }

    public async Task<Result<List<FarmCertificationDto>>> GetCertificationsAsync(Guid userId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result<List<FarmCertificationDto>>.Failure(FarmErrors.FarmNotFound);

        return Result<List<FarmCertificationDto>>.Success(
            farm.FarmCertifications.Select(MapCertificationDto).ToList());
    }

    public Task<Result> AddCertificationAsync(Guid userId, AddFarmCertificationRequest request)
    {
        _ = userId;
        _ = request;
        return Task.FromResult(Result.Failure(FarmErrors.CertificationForbidden));
    }

    public Task<Result> DeleteCertificationAsync(Guid userId, Guid certificationId)
    {
        _ = userId;
        _ = certificationId;
        return Task.FromResult(Result.Failure(FarmErrors.CertificationForbidden));
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
        FactoryId = m.SupplyRequest?.FactoryId,
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
        ContractId = m.Contract?.ContractId,
        ContractFullySigned = m.Contract?.Status == ContractStatus.Signed,
        CanMessage = MatchMessaging.CanMessage(m),
        NegotiationRounds = (m.NegotiationRounds ?? Array.Empty<MatchNegotiationRound>())
            .OrderBy(r => r.CreatedAt)
            .Select(r => new NileChain.Application.Dtos.Factory.MatchNegotiationRoundDto
            {
                RoundId = r.RoundId,
                OfferedBy = r.OfferedBy.ToString(),
                QuantityTons = r.QuantityTons,
                PricePerTon = r.PricePerTon,
                DeliveryDate = r.DeliveryDate,
                Grade = r.Grade,
                Note = r.Note,
                CreatedAt = r.CreatedAt
            })
            .ToList(),
        CounterQuantityTons = m.CounterQuantityTons,
        CounterPricePerTon = m.CounterPricePerTon,
        CounterDeliveryDate = m.CounterDeliveryDate,
        CounterNote = m.CounterNote,
        CounteredAt = m.CounteredAt,
        CounterAccepted = m.CounterAccepted,
        EffectiveQuantityTons = MatchCommercialTerms.QuantityTons(m),
        EffectivePricePerTon = MatchCommercialTerms.PricePerTon(m),
        EffectiveDeliveryDate = MatchCommercialTerms.DeliveryDate(m)
    };

    public async Task<Result> RespondToMatchAsync(Guid userId, Guid matchId, string action)
    {
        var match = await _farmMatchRepository.GetByIdAsync(matchId);
        if (match is null)
            return Result.Failure(FarmErrors.MatchNotFound);

        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null || match.FarmId != farm.FarmId)
            return Result.Failure(FarmErrors.MatchNotFound);

        if (match.Status != FarmMatchStatus.Proposed && match.Status != FarmMatchStatus.Countered)
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
        var qty = MatchCommercialTerms.QuantityTons(match);
        var price = MatchCommercialTerms.PricePerTon(match);
        var delivery = MatchCommercialTerms.DeliveryDate(match);
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
            ? "سعر الطن يُحدَّد كتابةً لاحقاً."
            : $"السعر: {price:N0} جنيه مصري للطن.";
        var deliveryLine = delivery is null
            ? "تاريخ التسليم يُتفق عليه بين الطرفين."
            : $"تاريخ التسليم: {delivery:yyyy-MM-dd}.";
        var qualityLine = string.IsNullOrWhiteSpace(quality)
            ? "يجب أن تستوفي البضاعة معايير الجودة المعتادة في السوق للمحصول."
            : $"مواصفات الجودة: {quality}";

        return
            $"""
            بسم الله الرحمن الرحيم

            عقد توريد زراعي

            الطرف الأول (المشتري / المصنع): {factoryName}
            الطرف الثاني (المورد / المزرعة): {farmName}

            المادة الأولى — موضوع العقد
            يتعهد المورد بتوريد {qty:N2} طن متري من محصول «{crop}» إلى المشتري وفق أحكام هذا العقد.

            المادة الثانية — الثمن والسداد
            {priceLine}
            يتم السداد بعد التسليم وتأكيد الفحص من المشتري، مع خضوع التأخر لآليات التسوية المعتمدة على منصة NileChain.

            المادة الثالثة — التسليم
            {deliveryLine}
            مكان التسليم: {location}.
            تنتقل مخاطر الهلاك عند قبول التسليم في المكان المحدد.

            المادة الرابعة — التزامات الطرفين
            يلتزم المورد بالحصاد والتعبئة والإرسال في المواعيد المتفق عليها، ويلتزم المشتري بتوفير طاقة الاستلام وإتمام الفحص خلال مدة معقولة.

            المادة الخامسة — الجودة
            {qualityLine}
            يجوز رفض الشحنات غير المطابقة أو تعديل السعر بحسب الضرر الفعلي.

            المادة السادسة — القوة القاهرة
            لا يُسأل أي طرف عن التأخر الناتج عن أحداث خارجة عن السيطرة المعقولة، بما في ذلك الأحوال الجوية القصوى، شريطة الإخطار الفوري.

            المادة السابعة — الإنهاء
            يجوز لأي طرف إنهاء العقد عند الإخلال الجوهري إذا لم يُعالَج خلال مدة معقولة بعد الإخطار الكتابي.
            رفض المسودة قبل التوقيع يلغي هذا المشروع دون مسؤولية تتجاوز تكاليف الاعتماد المعقولة.

            المادة الثامنة — القبول الإلكتروني
            بقبول هذا العقد عبر منصة NileChain يؤكد كل طرف أنه راجع البنود ويوافق على الالتزام بها، وأن التوقيع الإلكتروني عبر المنصة ملزم.
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
            Description = farm.Description,
            BankName = farm.BankName,
            AccountHolderName = farm.AccountHolderName,
            BankAccountMasked = MaskAccount(farm.BankAccountNumber),
            Iban = farm.Iban,
            IsVerified = farm.IsVerified,
            CompletionPercent = CalculateCompletionPercent(farm),
            CropTypes = farm.FarmCrops
                .OrderBy(c => c.CropType.Name)
                .Select(c => new CropTypeDto
                {
                    CropTypeId = c.CropTypeId,
                    Name = c.CropType.Name,
                    AvailableQuantityTons = c.AvailableQuantityTons,
                    AvailableFrom = c.AvailableFrom,
                    AvailableTo = c.AvailableTo,
                    MinPricePerTon = c.MinPricePerTon,
                    IsPublished = c.IsPublished
                }).ToList(),
            Certifications = farm.FarmCertifications
                .OrderBy(c => c.Certification.Name)
                .Select(MapCertificationDto)
                .ToList(),
            Documents = farm.FarmDocuments.Select(MapToDocumentDto).ToList(),
            Images = farm.FarmImages
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.UploadedAt)
                .Select(i => new FarmImageDto
                {
                    ImageId = i.FarmImageId,
                    FileName = i.FileName,
                    FileUrl = i.FileUrl,
                    SortOrder = i.SortOrder
                }).ToList()
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

    private static FarmCertificationDto MapCertificationDto(FarmCertification c)
    {
        var now = DateTime.UtcNow;
        return new FarmCertificationDto
        {
            CertificationId = c.CertificationId,
            Name = c.Certification?.Name ?? "Unknown",
            IssuedAt = c.IssuedAt,
            ExpiresAt = c.ExpiresAt,
            IsExpired = c.ExpiresAt is not null && c.ExpiresAt <= now
        };
    }

    private static bool IsValidCropCommercialTerms(
        decimal? availableQuantityTons,
        DateTime? availableFrom,
        DateTime? availableTo,
        decimal? minPricePerTon)
    {
        if (availableQuantityTons is < 0)
            return false;
        if (minPricePerTon is < 0)
            return false;
        if (availableFrom is not null && availableTo is not null && availableTo < availableFrom)
            return false;
        return true;
    }

    private static DateTime? NormalizeDate(DateTime? value) =>
        value?.Date;

    private async Task RequeueKybIfNeededAsync(Guid userId)
    {
        if (_userManager is null)
            return;

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.IsVerified)
            return;

        if (user.KybReviewStatus is KybReviewStatus.Rejected or KybReviewStatus.RequestInfo)
        {
            user.KybReviewStatus = KybReviewStatus.Pending;
            await _userManager.UpdateAsync(user);
        }
    }

    private static FarmDocumentDto MapToDocumentDto(FarmDocument document)
    {
        return new FarmDocumentDto
        {
            DocumentId = document.FarmDocumentId,
            Name = document.FileName,
            FileUrl = document.FileUrl,
            Size = FormatFileSize(document.FileSize),
            FileType = document.FileType,
            KybKind = document.KybKind.ToString()
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
        if (farm.FarmCrops.Count > 0) fields++;
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

        await EnsureTermDatesPersistedAsync(contract);
        return Result<FarmContractDto>.Success(MapContract(contract));
    }

    public async Task<Result<FarmContractDto>> ApproveContractAsync(
        Guid userId,
        Guid contractId,
        string? otpCode,
        string? ipAddress,
        string? userAgent,
        string? consentText)
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

        if (!MatchEligibilityGuard.IsStillEligible(match))
            return Result<FarmContractDto>.Failure(FarmErrors.EligibilityChanged);

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

        var spendAccept = ShouldSpendFarmAccept(match);
        if (spendAccept && _subscriptions is not null)
        {
            var quota = await _subscriptions.EnsureCanConsumeAsync(userId, SubscriptionMetric.FarmAccepts);
            if (quota.IsFailure)
                return Result<FarmContractDto>.Failure(quota.Error!);
        }

        if (_signingOtp is not null)
        {
            var otpResult = await _signingOtp.VerifyAndConsumeAsync(contractId, userId, otpCode);
            if (otpResult.IsFailure)
                return Result<FarmContractDto>.Failure(otpResult.Error!);
        }

        if (!ContractDealFunding.TryGetDealTotalEgp(match, out var dealTotalEgp))
            return Result<FarmContractDto>.Failure(WalletErrors.DealValueInvalid);

        var factoryId = match?.SupplyRequest?.FactoryId;
        if (factoryId is null || factoryId == Guid.Empty)
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotFound);

        // Completing signature: factory already signed → require funds then hold.
        var holdAmount = _walletService.GetDealHoldAmountEgp(dealTotalEgp);

        var completingFullSign = contract.IsFactorySigned;
        if (completingFullSign)
        {
            var fundsOk = await _walletService.EnsureFactoryAvailableAsync(factoryId.Value, holdAmount);
            if (fundsOk.IsFailure)
                return Result<FarmContractDto>.Failure(fundsOk.Error!);
        }

        var signedAt = DateTime.UtcNow;
        if (_signatures is not null && _contractHash is not null)
        {
            await AdvancedSignatureWriter.AppendAsync(
                _signatures,
                _contractHash,
                contract,
                userId,
                signedAt,
                ipAddress,
                userAgent,
                consentText ?? string.Empty,
                completingFullSign);
        }

        contract.FarmSignedAt = signedAt;
        // FactorySignedAt must remain unchanged.
        contract.RefreshSignatureStatus();
        ContractExecution.AcceptMatchIfFullySigned(contract);

        if (contract.IsFullySigned)
        {
            var delivery = MatchCommercialTerms.DeliveryDate(contract.FarmMatch)
                ?? contract.FarmMatch?.SupplyRequest?.DeliveryDate;
            ContractTermDates.ApplyOnFullSign(contract, delivery);
        }

        if (contract.IsFullySigned && !contract.HasDealFundsHeld)
        {
            var hold = await _walletService.HoldDealFundsAsync(
                factoryId.Value,
                contract.ContractId,
                holdAmount,
                $"Deal funds held on full signature for contract {contract.ContractId:N}");
            if (hold.IsFailure)
                return Result<FarmContractDto>.Failure(hold.Error!);

            contract.FundsHeldAt = DateTime.UtcNow;
            contract.FundsHeldEgp = holdAmount;
        }

        if (contract.IsFullySigned)
            await _integrity.AnchorIfFullySignedAsync(contract);

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

        if (spendAccept && _subscriptions is not null)
            await _subscriptions.ConsumeAsync(userId, SubscriptionMetric.FarmAccepts);

        if (contract.IsFullySigned && _email is not null && _logger is not null)
            await SignatureConfirmationEmail.TrySendToBothPartiesAsync(_email, _logger, contract);

        if (contract.IsFullySigned)
        {
            await _fulfillmentService.EnsureCreatedForSignedContractAsync(
                contract.ContractId,
                userId,
                contract.FarmMatch?.SupplyRequest?.DeliveryDate,
                contract.FarmMatch?.SupplyRequest?.DeliveryPoint,
                contract.FarmMatch?.SupplyRequest?.FreightPayer,
                contract.FarmMatch?.SupplyRequest?.TransitRisk);
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

        if (contract.Status == ContractStatus.Cancelled)
            return Result<FarmContractDto>.Failure(FarmErrors.ContractNotPending);

        if (contract.Status == ContractStatus.Signed)
        {
            var unwind = await _escrowPayments.UnwindSignedDealAsync(
                contract.ContractId,
                userId,
                "Signed contract cancelled by farm");
            if (unwind.IsFailure)
                return Result<FarmContractDto>.Failure(unwind.Error!);

            contract.Status = ContractStatus.Cancelled;
            await _integrity.SupersedeActiveAsync(contract.ContractId);
            _contractRepository.Update(contract);

            var factoryUser = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;
            if (factoryUser is Guid factoryUid && factoryUid != Guid.Empty)
            {
                await _notificationRepository.AddAsync(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = factoryUid,
                    Title = "Signed contract cancelled",
                    Message = $"{farm.Name} cancelled the signed supply contract. Held funds were returned to the factory wallet.",
                    Type = "ContractCancelled",
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
            }

            var unwindSaved = await TrySaveContractChangesAsync();
            if (unwindSaved.IsFailure)
                return Result<FarmContractDto>.Failure(unwindSaved.Error!);

            await _fulfillmentService.VoidForContractAsync(
                contract.ContractId,
                userId,
                "Signed contract cancelled by farm");
            return Result<FarmContractDto>.Success(MapContract(contract));
        }

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
        await _integrity.SupersedeActiveAsync(contract.ContractId);
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

        await EnsureTermDatesPersistedAsync(contract);
        var startsAt = contract.StartsAt ?? (contract.IsFullySigned ? contract.SignedAt : null);
        var endsAt = contract.EndsAt
            ?? (contract.FarmMatch is not null
                ? MatchCommercialTerms.DeliveryDate(contract.FarmMatch)
                : contract.FarmMatch?.SupplyRequest?.DeliveryDate);
        var text = ContractTermDates.FillPlaceholders(contract.GeneratedText, startsAt, endsAt)
            ?? string.Empty;
        var factory = contract.FarmMatch?.SupplyRequest?.Factory;
        var farmEntity = contract.FarmMatch?.Farm;
        var supply = contract.FarmMatch?.SupplyRequest;
        var delivery = contract.FarmMatch is not null
            ? MatchCommercialTerms.DeliveryDate(contract.FarmMatch)
            : supply?.DeliveryDate;
        var pdfModel = new NileChain.Application.Dtos.Contracts.ContractPdfModel
        {
            ContractId = contract.ContractId,
            Title = "Agricultural Supply Agreement",
            Status = contract.Status.ToString(),
            DocumentVersion = "1.0",
            CreatedAt = contract.CreatedAt,
            UpdatedAt = contract.SignedAt ?? contract.FarmSignedAt ?? contract.FactorySignedAt ?? contract.CreatedAt,
            StartsAt = startsAt,
            EndsAt = endsAt,
            DeliveryDate = delivery,
            FactoryName = factory?.Name ?? "Factory",
            FactoryLocation = factory?.Location ?? factory?.Governorate,
            FarmName = farmEntity?.Name ?? farm.Name,
            FarmLocation = farmEntity?.Location ?? farmEntity?.Governorate,
            CropName = supply?.CropType?.Name ?? string.Empty,
            QuantityTons = contract.FarmMatch is not null
                ? MatchCommercialTerms.QuantityTons(contract.FarmMatch)
                : supply?.QuantityTons ?? 0,
            PricePerTon = contract.FarmMatch is not null
                ? MatchCommercialTerms.PricePerTon(contract.FarmMatch)
                : supply?.PricePerTon,
            DeliveryLocation = factory?.Location ?? factory?.Governorate,
            QualityRequirements = NileChain.Application.Common.ContractQualitySummary.Format(supply?.QualitySpecs),
            PaymentTerms = ContractBodyParser.ExtractPaymentTermsHint(text),
            RiskScore = contract.FarmMatch?.RiskScore,
            GeneratedText = text,
            FactorySigned = contract.IsFactorySigned,
            FarmSigned = contract.IsFarmSigned,
            FactorySignedAt = contract.FactorySignedAt,
            FarmSignedAt = contract.FarmSignedAt
        };
        var bytes = _pdfService.GeneratePdf(pdfModel);

        // Do not persist role-scoped PdfUrl — download endpoints are authoritative.
        return Result<(byte[], string)>.Success((bytes, $"contract-{contract.ContractId:N}.pdf"));
    }

    private async Task EnsureTermDatesPersistedAsync(Contract contract)
    {
        if (!contract.IsFullySigned)
            return;

        var delivery = contract.FarmMatch is not null
            ? MatchCommercialTerms.DeliveryDate(contract.FarmMatch)
            : contract.FarmMatch?.SupplyRequest?.DeliveryDate;
        var beforeStart = contract.StartsAt;
        var beforeEnd = contract.EndsAt;
        var beforeText = contract.GeneratedText;

        ContractTermDates.EnsureApplied(contract, delivery);

        if (contract.StartsAt == beforeStart
            && contract.EndsAt == beforeEnd
            && contract.GeneratedText == beforeText)
        {
            return;
        }

        _contractRepository.Update(contract);
        await _unitOfWork.SaveChangesAsync();
    }

    private static FarmContractDto MapContract(Contract c)
    {
        var factory = c.FarmMatch?.SupplyRequest?.Factory;
        var farm = c.FarmMatch?.Farm;
        var supply = c.FarmMatch?.SupplyRequest;
        var delivery = c.FarmMatch is not null
            ? MatchCommercialTerms.DeliveryDate(c.FarmMatch)
            : supply?.DeliveryDate;
        var startsAt = c.StartsAt ?? (c.IsFullySigned ? c.SignedAt : null);
        var endsAt = c.EndsAt ?? delivery;
        return new FarmContractDto
        {
            ContractId = c.ContractId,
            MatchId = c.MatchId,
            FactoryName = factory?.Name ?? "Unknown",
            FactoryLocation = factory?.Location ?? factory?.Governorate,
            FarmName = farm?.Name ?? "Unknown",
            FarmLocation = farm?.Location ?? farm?.Governorate,
            CropName = supply?.CropType?.Name ?? "Unknown",
            QuantityTons = c.FarmMatch is not null
                ? MatchCommercialTerms.QuantityTons(c.FarmMatch)
                : supply?.QuantityTons ?? 0,
            PricePerTon = c.FarmMatch is not null
                ? MatchCommercialTerms.PricePerTon(c.FarmMatch)
                : supply?.PricePerTon,
            QualityRequirements = NileChain.Application.Common.ContractQualitySummary.Format(supply?.QualitySpecs),
            DeliveryDate = delivery,
            StartsAt = startsAt,
            EndsAt = endsAt,
            HasPendingDateAmendment = c.HasPendingDateAmendment,
            PendingStartsAt = c.PendingStartsAt,
            PendingEndsAt = c.PendingEndsAt,
            DateAmendmentProposedByUserId = c.DateAmendmentProposedByUserId,
            DeliveryLocation = factory?.Location ?? factory?.Governorate,
            GeneratedText = ContractTermDates.FillPlaceholders(c.GeneratedText, startsAt, endsAt),
            PdfUrl = c.PdfUrl,
            Status = c.Status.ToString(),
            CreatedAt = c.CreatedAt,
            SignedAt = c.SignedAt,
            FactorySigned = c.IsFactorySigned,
            FarmSigned = c.IsFarmSigned,
            FactorySignedAt = c.FactorySignedAt,
            FarmSignedAt = c.FarmSignedAt,
            FarmUserId = farm?.UserId,
            FactoryUserId = factory?.UserId,
            CanUnwindSigned = c.Status == ContractStatus.Signed
                && (c.Fulfillment is null
                    || c.Fulfillment.Status is FulfillmentStatus.Planned or FulfillmentStatus.Shipped),
            UpdatedAt = c.SignedAt ?? c.FarmSignedAt ?? c.FactorySignedAt ?? c.CreatedAt,
            MatchScore = c.FarmMatch?.MatchScore,
            RiskScore = c.FarmMatch?.RiskScore,
            Integrity = ContractIntegrityService.MapActive(c),
            LastRevision = ContractRevisionDto.Last(c.Revisions)
        };
    }

    private static bool FarmHasCommittedToMatch(FarmMatch? match) =>
        match?.NegotiationRounds?.Any(r => r.OfferedBy == DealParty.Farm) == true;

    private static bool ShouldSpendFarmAccept(FarmMatch? match) =>
        match is not null
        && match.Status is FarmMatchStatus.Proposed or FarmMatchStatus.Countered
        && !FarmHasCommittedToMatch(match);

    private static bool IsPartyActive(NileChain.Domain.Identity.ApplicationUser? user) =>
        user is null || user.IsActive;

    public async Task<Result<List<ConversationDto>>> GetConversationsAsync(Guid userId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<List<ConversationDto>>.Failure(FarmErrors.FarmNotFound);

        var matches = await _farmRepository.GetConversationsAsync(userId);

        var dtos = matches
            .Where(m => m.Contract is not null && m.Contract.Status == ContractStatus.Signed)
            .Select(m =>
        {
            var lastMsg = m.Messages.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
            return new ConversationDto
            {
                MatchId = m.MatchId,
                FactoryId = m.SupplyRequest?.FactoryId,
                FactoryName = m.SupplyRequest?.Factory?.Name ?? "Unknown",
                CropName = m.SupplyRequest?.CropType?.Name,
                Status = m.Status.ToString(),
                MatchCreatedAt = m.CreatedAt,
                QuantityTons = MatchCommercialTerms.QuantityTons(m),
                PricePerTon = MatchCommercialTerms.PricePerTon(m),
                DeliveryDate = MatchCommercialTerms.DeliveryDate(m),
                ContractId = m.Contract?.ContractId,
                ContractFullySigned = true,
                LastMessage = lastMsg?.Content,
                LastMessageAt = lastMsg?.CreatedAt,
                UnreadCount = m.Messages.Count(x => !x.IsRead && x.ReceiverId == farm.UserId)
            };
        }).OrderByDescending(d => d.LastMessageAt ?? d.MatchCreatedAt).ToList();

        return Result<List<ConversationDto>>.Success(dtos);
    }

    public async Task<Result<List<MessageDto>>> GetMessagesAsync(Guid userId, Guid matchId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<List<MessageDto>>.Failure(FarmErrors.FarmNotFound);

        var match = await _farmRepository.GetFarmMatchByIdAsync(userId, matchId);
        if (match is null)
            return Result<List<MessageDto>>.Failure(FarmErrors.ConversationNotFound);

        if (!MatchMessaging.CanMessage(match))
            return Result<List<MessageDto>>.Failure(FarmErrors.CannotSendMessage);

        var messages = await _farmRepository.GetMessagesAsync(userId, matchId);

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

        if (!MatchMessaging.CanMessage(match))
            return Result.Failure(FarmErrors.CannotSendMessage);

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

    public async Task<Result<FactoryPublicProfileDto>> GetMatchedFactoryPublicProfileAsync(
        Guid userId,
        Guid factoryId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<FactoryPublicProfileDto>.Failure(FarmErrors.FarmNotFound);

        var matches = await _farmRepository.GetFarmMatchesAsync(userId, null, null);
        var related = matches
            .Where(m => m.SupplyRequest?.FactoryId == factoryId)
            .OrderByDescending(m => m.CreatedAt)
            .ToList();

        if (related.Count == 0)
            return Result<FactoryPublicProfileDto>.Failure(FarmErrors.FactoryProfileUnavailable);

        var factory = related[0].SupplyRequest?.Factory;
        if (factory is null)
            return Result<FactoryPublicProfileDto>.Failure(FarmErrors.FactoryProfileUnavailable);

        var active = related
            .Where(m => m.Status is not FarmMatchStatus.Rejected and not FarmMatchStatus.Expired)
            .FirstOrDefault()
            ?? related[0];

        return Result<FactoryPublicProfileDto>.Success(new FactoryPublicProfileDto
        {
            FactoryId = factory.FactoryId,
            Name = factory.Name,
            Governorate = factory.Governorate,
            Location = factory.Location,
            IndustryType = factory.IndustryType,
            IsVerified = factory.IsVerified,
            AverageRating = factory.AverageRating,
            RatingCount = factory.RatingCount,
            ActiveMatchId = active.MatchId,
            ActiveContractId = active.Contract?.ContractId,
            ContractFullySigned = active.Contract?.Status == ContractStatus.Signed,
            CanMessage = MatchMessaging.CanMessage(active)
        });
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
