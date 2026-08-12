using NileChain.Application.Common;
using NileChain.Application.Contracts;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Matching;
using NileChain.Application.Options;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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
    private readonly IWalletService _walletService;
    private readonly IMockEscrowPaymentService _escrowPayments;
    private readonly IContractIntegrityService _integrity;
    private readonly IUnitOfWork _unitOfWork;
    private readonly DeliveryTermsOptions _deliveryTerms;

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
        IWalletService walletService,
        IMockEscrowPaymentService escrowPayments,
        IContractIntegrityService integrity,
        IUnitOfWork unitOfWork,
        IOptions<DeliveryTermsOptions>? deliveryTerms = null)
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
        _walletService = walletService;
        _escrowPayments = escrowPayments;
        _integrity = integrity;
        _unitOfWork = unitOfWork;
        _deliveryTerms = deliveryTerms?.Value ?? new DeliveryTermsOptions();
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

        // Normalize governorate slugs (giza → Giza) so MatchingPlugin hard filters work.
        var normalizedGovs = (request.SelectedGovernorates ?? new List<string>())
            .Select(NormalizeGovernorateLabel)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var scope = string.IsNullOrWhiteSpace(request.GeographicScope)
            ? (normalizedGovs.Count > 0 || !string.IsNullOrWhiteSpace(factory.Governorate)
                ? "Exact"
                : "Nationwide")
            : request.GeographicScope.Trim();

        var quality = StructuredQualitySpecs.Pack(
            request.Quality,
            request.StructuredQuality,
            normalizedGovs!,
            scope,
            request.PreferredFarmId);

        var terms = ResolveDeliveryTerms(
            request.DeliveryPoint,
            request.FreightPayer,
            request.TransitRisk,
            requirePoint: _deliveryTerms.Required);
        if (terms.IsFailure)
            return Result<CreateSupplyRequestResponse>.Failure(terms.Error!);

        var entity = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = crop.CropTypeId,
            QuantityTons = request.Quantity,
            PricePerTon = request.Price,
            DeliveryDate = DeliveryDatePolicy.ToUtcStorage(request.DeliveryDate),
            QualitySpecs = quality,
            DeliveryPoint = terms.Value.Point,
            FreightPayer = terms.Value.Freight,
            TransitRisk = terms.Value.Transit,
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
        int pageSize = 10,
        string? status = null)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<PagedResult<FactorySupplyRequestListItemDto>>.Failure(FactoryErrors.FactoryNotFound);

        var (items, total) = await _factoryRepository.GetSupplyRequestsPagedAsync(
            factory.FactoryId,
            page,
            pageSize,
            status);

        var dtos = items.Select(MapListItem).ToList();

        return Result<PagedResult<FactorySupplyRequestListItemDto>>.Success(new PagedResult<FactorySupplyRequestListItemDto>
        {
            Items = dtos,
            TotalCount = total,
            Page = Math.Max(1, page),
            PageSize = Math.Clamp(pageSize, 1, 100)
        });
    }

    public async Task<Result<FactorySupplyRequestDetailDto>> GetRequestAsync(Guid userId, Guid requestId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<FactorySupplyRequestDetailDto>.Failure(FactoryErrors.FactoryNotFound);

        var request = await _factoryRepository.GetSupplyRequestDetailAsync(factory.FactoryId, requestId);
        if (request is null)
            return Result<FactorySupplyRequestDetailDto>.Failure(FactoryErrors.SupplyRequestNotFound);

        return Result<FactorySupplyRequestDetailDto>.Success(MapDetail(request));
    }

    public async Task<Result<FactorySupplyRequestDetailDto>> UpdateRequestDeliveryTermsAsync(
        Guid userId,
        Guid requestId,
        UpdateSupplyRequestDeliveryTermsRequest body)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<FactorySupplyRequestDetailDto>.Failure(FactoryErrors.FactoryNotFound);

        var request = await _factoryRepository.GetSupplyRequestDetailAsync(factory.FactoryId, requestId);
        if (request is null)
            return Result<FactorySupplyRequestDetailDto>.Failure(FactoryErrors.SupplyRequestNotFound);

        if (request.Status is SupplyRequestStatus.Cancelled or SupplyRequestStatus.Fulfilled)
            return Result<FactorySupplyRequestDetailDto>.Failure(FactoryErrors.SupplyRequestCannotUpdate);

        var hasSigned = request.FarmMatches.Any(m =>
            m.Contract is not null
            && m.Contract.Status == ContractStatus.Signed
            && m.Contract.IsFullySigned);
        if (hasSigned)
            return Result<FactorySupplyRequestDetailDto>.Failure(FactoryErrors.SupplyRequestCannotUpdate);

        var terms = ResolveDeliveryTerms(
            body.DeliveryPoint ?? request.DeliveryPoint.ToString(),
            body.FreightPayer ?? request.FreightPayer.ToString(),
            body.TransitRisk ?? request.TransitRisk.ToString(),
            requirePoint: true);
        if (terms.IsFailure)
            return Result<FactorySupplyRequestDetailDto>.Failure(terms.Error!);

        request.DeliveryPoint = terms.Value.Point;
        request.FreightPayer = terms.Value.Freight;
        request.TransitRisk = terms.Value.Transit;
        _supplyRequestRepository.Update(request);
        await _unitOfWork.SaveChangesAsync();

        var fresh = await _factoryRepository.GetSupplyRequestDetailAsync(factory.FactoryId, requestId);
        return Result<FactorySupplyRequestDetailDto>.Success(MapDetail(fresh ?? request));
    }

    public async Task<Result> CancelRequestAsync(Guid userId, Guid requestId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result.Failure(FactoryErrors.FactoryNotFound);

        var request = await _factoryRepository.GetSupplyRequestDetailAsync(factory.FactoryId, requestId);
        if (request is null)
            return Result.Failure(FactoryErrors.SupplyRequestNotFound);

        if (request.Status is SupplyRequestStatus.Cancelled or SupplyRequestStatus.Fulfilled)
            return Result.Failure(FactoryErrors.SupplyRequestCannotCancel);

        var hasSigned = request.FarmMatches.Any(m =>
            m.Contract is not null
            && m.Contract.Status == ContractStatus.Signed
            && m.Contract.IsFullySigned);
        if (hasSigned)
            return Result.Failure(FactoryErrors.SupplyRequestCannotCancel);

        foreach (var match in request.FarmMatches)
        {
            if (match.Status is FarmMatchStatus.Proposed
                or FarmMatchStatus.Countered
                or FarmMatchStatus.Accepted)
            {
                match.Status = FarmMatchStatus.Rejected;
                _farmMatchRepository.Update(match);
            }

            var contract = match.Contract;
            if (contract is not null
                && contract.Status is ContractStatus.PendingSignature
                    or ContractStatus.Draft
                    or ContractStatus.PendingFarmSignature
                    or ContractStatus.PendingFactorySignature)
            {
                contract.Status = ContractStatus.Cancelled;
                contract.ClearSignatures();
                _contractRepository.Update(contract);
                await _fulfillmentService.VoidForContractAsync(
                    contract.ContractId, userId, "Supply request cancelled");
                await _paymentMilestoneService.VoidForContractAsync(
                    contract.ContractId, userId, "Supply request cancelled");
            }
        }

        request.Status = SupplyRequestStatus.Cancelled;
        _supplyRequestRepository.Update(request);
        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<FactoryDashboardResponse>> GetDashboardAsync(Guid userId)
    {
        var factory = await _factoryRepository.GetFactoryWithDashboardDataAsync(userId);
        if (factory is null)
            return Result<FactoryDashboardResponse>.Failure(FactoryErrors.FactoryNotFound);

        var requests = factory.SupplyRequests?.ToList() ?? new List<SupplyRequest>();
        var matches = requests.SelectMany(r => r.FarmMatches ?? Array.Empty<FarmMatch>()).ToList();
        var contracts = matches
            .Where(m => m.Contract is not null)
            .Select(m => m.Contract!)
            .ToList();

        var openRequests = requests.Count(r =>
            r.Status is SupplyRequestStatus.Pending or SupplyRequestStatus.Matched);
        var activeMatches = matches.Count(m =>
            m.Status is FarmMatchStatus.Proposed
                or FarmMatchStatus.Countered
                or FarmMatchStatus.Accepted);
        var activeContracts = contracts.Count(c =>
            c.Status is ContractStatus.PendingSignature
                or ContractStatus.PendingFarmSignature
                or ContractStatus.PendingFactorySignature
                or ContractStatus.Signed);
        var completed = contracts.Count(c =>
            c.Status == ContractStatus.Signed
            && c.IsFullySigned
            && c.Fulfillment?.Status == FulfillmentStatus.Fulfilled);

        var procurementValue = requests
            .Where(r => r.Status != SupplyRequestStatus.Cancelled)
            .Sum(r => (r.PricePerTon ?? 0m) * r.QuantityTons);

        var riskScores = matches
            .Where(m => m.RiskScore is not null)
            .Select(m => m.RiskScore!.Value)
            .ToList();

        var attention = BuildFactoryAttention(requests, matches, contracts);
        var payables = BuildFactoryPayables(contracts);

        var recent = requests
            .OrderByDescending(r => r.CreatedAt)
            .Take(8)
            .Select(MapListItem)
            .ToList();

        return Result<FactoryDashboardResponse>.Success(new FactoryDashboardResponse
        {
            OpenRequestsCount = openRequests,
            ActiveMatchesCount = activeMatches,
            ActiveContractsCount = activeContracts,
            CompletedContractsCount = completed,
            TotalProcurementValue = procurementValue,
            AverageSupplierRiskScore = riskScores.Count == 0
                ? 0
                : Math.Round(riskScores.Average(), 1),
            PayablesSummary = payables,
            Attention = attention,
            RecentRequests = recent
        });
    }

    public async Task<Result<FactorySupplierScorecardDto>> GetSupplierScorecardAsync(
        Guid userId,
        Guid farmId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<FactorySupplierScorecardDto>.Failure(FactoryErrors.FactoryNotFound);

        var matches = await _factoryRepository.GetMatchesWithFarmForFactoryAsync(
            factory.FactoryId,
            farmId);
        if (matches.Count == 0)
            return Result<FactorySupplierScorecardDto>.Failure(FactoryErrors.FarmNotFound);

        var farm = matches[0].Farm;
        var signed = matches
            .Where(m => m.Contract is not null
                && m.Contract.Status == ContractStatus.Signed
                && m.Contract.IsFullySigned)
            .ToList();

        var disputes = signed
            .SelectMany(m => m.Contract?.Disputes ?? Array.Empty<Dispute>())
            .ToList();

        var onTime = BuildOnTimeRate(signed);
        var qcIssue = BuildQcIssueRate(signed);
        var avgDiscount = BuildAvgQcDiscount(signed);

        return Result<FactorySupplierScorecardDto>.Success(new FactorySupplierScorecardDto
        {
            FarmId = farmId,
            FarmUserId = farm?.UserId,
            FarmName = farm?.Name ?? "Unknown",
            Governorate = farm?.Governorate,
            IsVerified = farm?.IsVerified ?? false,
            RiskScore = farm?.RiskScore ?? matches.Select(m => m.RiskScore).FirstOrDefault(s => s is not null),
            AverageRating = farm?.AverageRating ?? 0,
            RatingCount = farm?.RatingCount ?? 0,
            DealsWithThisFactory = matches.Count,
            CompletedContracts = signed.Count(m =>
                m.Contract?.Fulfillment?.Status == FulfillmentStatus.Fulfilled),
            OpenDisputes = disputes.Count(d =>
                d.Status is DisputeStatus.Open or DisputeStatus.UnderReview),
            TotalDisputes = disputes.Count,
            OnTimeFulfillmentRate = onTime,
            QcIssueRate = qcIssue,
            AverageQcDiscountPercent = avgDiscount,
            RecentDeals = signed
                .OrderByDescending(m => m.Contract!.CreatedAt)
                .Take(8)
                .Select(m => new FactorySupplierDealDto
                {
                    ContractId = m.Contract!.ContractId,
                    MatchId = m.MatchId,
                    Crop = m.SupplyRequest?.CropType?.Name ?? string.Empty,
                    QuantityTons = m.SupplyRequest?.QuantityTons ?? 0,
                    ContractStatus = m.Contract.Status.ToString(),
                    FulfillmentStatus = m.Contract.Fulfillment?.Status.ToString(),
                    SignedAt = m.Contract.CreatedAt,
                    QcDiscountPercent = m.Contract.Fulfillment?.DiscountPercent
                })
                .ToList()
        });
    }

    private static FactorySupplyRequestListItemDto MapListItem(SupplyRequest r)
    {
        var quality = StructuredQualitySpecs.Parse(r.QualitySpecs);
        var matches = r.FarmMatches ?? Array.Empty<FarmMatch>();
        var active = matches.Count(m =>
            m.Status is FarmMatchStatus.Proposed
                or FarmMatchStatus.Countered
                or FarmMatchStatus.Accepted);

        return new FactorySupplyRequestListItemDto
        {
            RequestId = r.RequestId,
            CropTypeId = r.CropTypeId,
            Crop = r.CropType?.Name ?? string.Empty,
            QuantityTons = r.QuantityTons,
            PricePerTon = r.PricePerTon,
            DeliveryDate = r.DeliveryDate,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt,
            IdempotencyKey = r.IdempotencyKey,
            MatchCount = matches.Count,
            ActiveMatchCount = active,
            GeographicScope = quality.GeographicScope,
            Quality = quality
        };
    }

    private static FactorySupplyRequestDetailDto MapDetail(SupplyRequest r)
    {
        var quality = StructuredQualitySpecs.Parse(r.QualitySpecs);
        var matches = r.FarmMatches ?? Array.Empty<FarmMatch>();
        var active = matches.Count(m =>
            m.Status is FarmMatchStatus.Proposed
                or FarmMatchStatus.Countered
                or FarmMatchStatus.Accepted);
        var hasSigned = matches.Any(m =>
            m.Contract is not null
            && m.Contract.Status == ContractStatus.Signed
            && m.Contract.IsFullySigned);
        var canCancel = r.Status is SupplyRequestStatus.Pending or SupplyRequestStatus.Matched
            && !hasSigned;
        var canRerun = r.Status is SupplyRequestStatus.Pending or SupplyRequestStatus.Matched;

        return new FactorySupplyRequestDetailDto
        {
            RequestId = r.RequestId,
            CropTypeId = r.CropTypeId,
            Crop = r.CropType?.Name ?? string.Empty,
            QuantityTons = r.QuantityTons,
            PricePerTon = r.PricePerTon,
            DeliveryDate = r.DeliveryDate,
            Status = r.Status.ToString(),
            CreatedAt = r.CreatedAt,
            IdempotencyKey = r.IdempotencyKey,
            QualitySpecsRaw = r.QualitySpecs,
            Quality = quality,
            MatchCount = matches.Count,
            ActiveMatchCount = active,
            CanCancel = canCancel,
            CanRerunAgent = canRerun,
            CanUpdateDeliveryTerms = canCancel,
            DeliveryPoint = r.DeliveryPoint.ToString(),
            FreightPayer = r.FreightPayer.ToString(),
            TransitRisk = r.TransitRisk.ToString()
        };
    }

    private static Result<(DeliveryPoint Point, DealParty Freight, DealParty Transit)> ResolveDeliveryTerms(
        string? deliveryPoint,
        string? freightPayer,
        string? transitRisk,
        bool requirePoint)
    {
        if (requirePoint && string.IsNullOrWhiteSpace(deliveryPoint))
            return Result<(DeliveryPoint, DealParty, DealParty)>.Failure(FactoryErrors.DeliveryTermsRequired);

        DeliveryPoint? point = null;
        if (!string.IsNullOrWhiteSpace(deliveryPoint))
        {
            if (!DeliveryTermsPolicy.TryParsePoint(deliveryPoint, out var parsedPoint))
                return Result<(DeliveryPoint, DealParty, DealParty)>.Failure(FactoryErrors.InvalidDeliveryTerms);
            point = parsedPoint;
        }

        DealParty? freight = null;
        if (!string.IsNullOrWhiteSpace(freightPayer))
        {
            if (!DeliveryTermsPolicy.TryParseParty(freightPayer, out var parsedFreight))
                return Result<(DeliveryPoint, DealParty, DealParty)>.Failure(FactoryErrors.InvalidDeliveryTerms);
            freight = parsedFreight;
        }

        DealParty? transit = null;
        if (!string.IsNullOrWhiteSpace(transitRisk))
        {
            if (!DeliveryTermsPolicy.TryParseParty(transitRisk, out var parsedTransit))
                return Result<(DeliveryPoint, DealParty, DealParty)>.Failure(FactoryErrors.InvalidDeliveryTerms);
            transit = parsedTransit;
        }

        var resolved = DeliveryTermsPolicy.Resolve(point, freight, transit);
        return Result<(DeliveryPoint, DealParty, DealParty)>.Success(
            (resolved.Point, resolved.FreightPayer, resolved.TransitRisk));
    }

    private static List<FactoryAttentionItemDto> BuildFactoryAttention(
        IReadOnlyList<SupplyRequest> requests,
        IReadOnlyList<FarmMatch> matches,
        IReadOnlyList<Contract> contracts)
    {
        var items = new List<FactoryAttentionItemDto>();

        var awaitingSignature = contracts.Count(c =>
            !c.IsFactorySigned
            && c.Status is ContractStatus.PendingSignature
                or ContractStatus.PendingFactorySignature
                or ContractStatus.PendingFarmSignature
                or ContractStatus.Draft);
        if (awaitingSignature > 0)
        {
            items.Add(new FactoryAttentionItemDto
            {
                Id = "sign",
                Kind = "signature",
                Tone = "attention",
                Count = awaitingSignature,
                Title = "Contracts awaiting your signature",
                Status = $"{awaitingSignature} pending",
                Cta = "Review contracts",
                Link = "/factory/contracts"
            });
        }

        var counters = matches.Count(m => m.Status == FarmMatchStatus.Countered);
        if (counters > 0)
        {
            items.Add(new FactoryAttentionItemDto
            {
                Id = "counter",
                Kind = "counter",
                Tone = "attention",
                Count = counters,
                Title = "Counter-offers waiting",
                Status = $"{counters} open",
                Cta = "Review counters",
                Link = "/factory/matches"
            });
        }

        var receiveQc = contracts.Count(c =>
            c.Fulfillment is not null
            && c.Fulfillment.Status is FulfillmentStatus.Shipped or FulfillmentStatus.Received);
        if (receiveQc > 0)
        {
            items.Add(new FactoryAttentionItemDto
            {
                Id = "receive-qc",
                Kind = "fulfillment",
                Tone = "attention",
                Count = receiveQc,
                Title = "Shipments to receive or QC",
                Status = $"{receiveQc} awaiting action",
                Cta = "Open fulfillment",
                Link = "/factory/contracts"
            });
        }

        var overdue = contracts
            .SelectMany(c => c.Transactions ?? Array.Empty<Transaction>())
            .Count(t =>
                t.DueDate is not null
                && t.DueDate.Value.Date < DateTime.UtcNow.Date
                && t.Status == TransactionStatus.Pending);
        if (overdue > 0)
        {
            items.Add(new FactoryAttentionItemDto
            {
                Id = "overdue-pay",
                Kind = "payment",
                Tone = "attention",
                Count = overdue,
                Title = "Overdue payment milestones",
                Status = $"{overdue} overdue",
                Cta = "Mark paid",
                Link = "/factory/contracts"
            });
        }

        var disputes = contracts
            .SelectMany(c => c.Disputes ?? Array.Empty<Dispute>())
            .Count(d => d.Status is DisputeStatus.Open or DisputeStatus.UnderReview);
        if (disputes > 0)
        {
            items.Add(new FactoryAttentionItemDto
            {
                Id = "disputes",
                Kind = "dispute",
                Tone = "attention",
                Count = disputes,
                Title = "Open disputes",
                Status = $"{disputes} active",
                Cta = "Review disputes",
                Link = "/factory/contracts"
            });
        }

        var pendingRequests = requests.Count(r => r.Status == SupplyRequestStatus.Pending);
        if (pendingRequests > 0 && items.Count < 5)
        {
            items.Add(new FactoryAttentionItemDto
            {
                Id = "pending-requests",
                Kind = "request",
                Tone = "info",
                Count = pendingRequests,
                Title = "Supply requests without matches",
                Status = $"{pendingRequests} pending",
                Cta = "View requests",
                Link = "/factory/requests"
            });
        }

        return items.Take(5).ToList();
    }

    private static FactoryPayablesSummaryDto BuildFactoryPayables(IReadOnlyList<Contract> contracts)
    {
        var txs = contracts
            .Where(c => c.Status == ContractStatus.Signed && c.IsFullySigned)
            .SelectMany(c => c.Transactions ?? Array.Empty<Transaction>())
            .Where(t => t.Status != TransactionStatus.Voided)
            .ToList();

        return new FactoryPayablesSummaryDto
        {
            PendingAmount = txs.Where(t => t.Status == TransactionStatus.Pending).Sum(t => t.Amount),
            AwaitingFarmConfirmAmount = txs.Where(t => t.Status == TransactionStatus.MarkedPaid).Sum(t => t.Amount),
            PaidConfirmedAmount = txs.Where(t => t.Status == TransactionStatus.Completed).Sum(t => t.Amount),
            OverdueAmount = txs
                .Where(t =>
                    t.Status == TransactionStatus.Pending
                    && t.DueDate is not null
                    && t.DueDate.Value.Date < DateTime.UtcNow.Date)
                .Sum(t => t.Amount)
        };
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
            if (fulfillment.ReceivedAt.Value.Date <= delivery.Value.Date.AddDays(3))
                onTime++;
        }

        return scored == 0 ? null : Math.Round((decimal)onTime / scored * 100m, 1);
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
                || f.SpecsMet == false
                || (f.AcceptedQuantityTons is not null
                    && requested is not null
                    && f.AcceptedQuantityTons < requested))
                issues++;
        }

        return withQc == 0 ? null : Math.Round((decimal)issues / withQc * 100m, 1);
    }

    private static decimal? BuildAvgQcDiscount(IReadOnlyList<FarmMatch> signedMatches)
    {
        var discounts = signedMatches
            .Select(m => m.Contract?.Fulfillment)
            .Where(f => f is not null && f.QualityCheckedAt is not null)
            .Select(f => f!.DiscountPercent)
            .ToList();
        return discounts.Count == 0 ? null : Math.Round(discounts.Average(), 1);
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
                CreatedAt = m.CreatedAt,
                ContractId = m.Contract?.ContractId,
                ContractFullySigned = m.Contract?.Status == ContractStatus.Signed,
                CanMessage = m.Contract?.Status == ContractStatus.Signed,
                RequestQuantityTons = m.SupplyRequest?.QuantityTons,
                RequestPricePerTon = m.SupplyRequest?.PricePerTon,
                RequestDeliveryDate = m.SupplyRequest?.DeliveryDate,
                CounterQuantityTons = m.CounterQuantityTons,
                CounterPricePerTon = m.CounterPricePerTon,
                CounterDeliveryDate = m.CounterDeliveryDate,
                CounterNote = m.CounterNote,
                CounterAccepted = m.CounterAccepted,
                EffectiveQuantityTons = MatchCommercialTerms.QuantityTons(m),
                EffectivePricePerTon = MatchCommercialTerms.PricePerTon(m),
                EffectiveDeliveryDate = MatchCommercialTerms.DeliveryDate(m)
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

        if (match.Status != FarmMatchStatus.Proposed && match.Status != FarmMatchStatus.Countered)
            return Result.Failure(FactoryErrors.MatchCannotExclude);

        match.Status = FarmMatchStatus.Rejected;
        match.IsExcludedByFactory = true;
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

        var farmUserId = match.Farm?.UserId;
        if (farmUserId is Guid uid && uid != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = "notifications.types.matchExcluded.title",
                Message = "notifications.types.matchExcluded.body",
                Type = "MatchExcluded",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> AcceptCounterOfferAsync(Guid userId, Guid matchId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result.Failure(FactoryErrors.FactoryNotFound);

        var match = await _factoryRepository.GetMatchForFactoryAsync(factory.FactoryId, matchId);
        if (match is null)
            return Result.Failure(FactoryErrors.MatchNotFound);

        if (match.Status != FarmMatchStatus.Countered || !MatchCommercialTerms.HasCounter(match))
            return Result.Failure(FactoryErrors.MatchNotCountered);

        match.CounterAccepted = true;
        match.Status = FarmMatchStatus.Proposed;
        _farmMatchRepository.Update(match);

        if (match.Farm?.UserId is Guid farmUserId && farmUserId != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = farmUserId,
                Title = "Counter-offer accepted",
                Message = $"{factory.Name} accepted your counter terms. You can proceed to the contract.",
                Type = "Match",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result> RejectCounterOfferAsync(Guid userId, Guid matchId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result.Failure(FactoryErrors.FactoryNotFound);

        var match = await _factoryRepository.GetMatchForFactoryAsync(factory.FactoryId, matchId);
        if (match is null)
            return Result.Failure(FactoryErrors.MatchNotFound);

        if (match.Status != FarmMatchStatus.Countered)
            return Result.Failure(FactoryErrors.MatchNotCountered);

        match.Status = FarmMatchStatus.Rejected;
        match.CounterAccepted = false;
        _farmMatchRepository.Update(match);

        if (match.Farm?.UserId is Guid farmUserId && farmUserId != Guid.Empty)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = farmUserId,
                Title = "Counter-offer rejected",
                Message = $"{factory.Name} rejected your counter terms.",
                Type = "Match",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();
        return Result.Success();
    }

    public async Task<Result<List<FarmListingDto>>> GetPublishedListingsAsync(
        Guid? cropTypeId = null,
        string? governorate = null)
    {
        var crops = await _factoryRepository.GetPublishedFarmCropsAsync(cropTypeId, governorate);
        var listings = crops.Select(fc => new FarmListingDto
        {
            FarmId = fc.FarmId,
            FarmName = fc.Farm.Name,
            Governorate = fc.Farm.Governorate,
            IsVerified = fc.Farm.IsVerified,
            RiskScore = fc.Farm.RiskScore,
            AverageRating = fc.Farm.AverageRating,
            CropTypeId = fc.CropTypeId,
            CropName = fc.CropType.Name,
            AvailableQuantityTons = fc.AvailableQuantityTons,
            AvailableFrom = fc.AvailableFrom,
            AvailableTo = fc.AvailableTo,
            MinPricePerTon = fc.MinPricePerTon,
            CoverImageUrl = fc.Farm.FarmImages
                .OrderBy(i => i.SortOrder)
                .Select(i => i.FileUrl)
                .FirstOrDefault()
        }).ToList();

        return Result<List<FarmListingDto>>.Success(listings);
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
        var dtos = matches
            .Where(m => m.Contract is not null && m.Contract.Status == ContractStatus.Signed)
            .Select(m =>
        {
            var lastMsg = m.Messages.OrderByDescending(x => x.CreatedAt).FirstOrDefault();
            return new FactoryConversationDto
            {
                MatchId = m.MatchId,
                FarmId = m.FarmId,
                FarmName = m.Farm?.Name ?? "Unknown",
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
                UnreadCount = m.Messages.Count(x => !x.IsRead && x.ReceiverId == factory.UserId)
            };
        }).OrderByDescending(d => d.LastMessageAt ?? d.MatchCreatedAt).ToList();

        return Result<List<FactoryConversationDto>>.Success(dtos);
    }

    public async Task<Result<FactoryActiveMatchDto>> GetActiveMatchWithFarmAsync(Guid userId, Guid farmId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<FactoryActiveMatchDto>.Failure(FactoryErrors.FactoryNotFound);

        var matches = await _factoryRepository.GetMatchesWithFarmForFactoryAsync(
            factory.FactoryId,
            farmId);
        var match = matches
            .Where(m => m.Status is not FarmMatchStatus.Rejected and not FarmMatchStatus.Expired)
            .OrderByDescending(m => m.CreatedAt)
            .FirstOrDefault()
            ?? matches.OrderByDescending(m => m.CreatedAt).FirstOrDefault();

        if (match is null)
            return Result<FactoryActiveMatchDto>.Failure(FactoryErrors.MatchNotFound);

        return Result<FactoryActiveMatchDto>.Success(new FactoryActiveMatchDto
        {
            MatchId = match.MatchId,
            FarmId = farmId,
            FarmName = match.Farm?.Name ?? "Unknown",
            CropName = match.SupplyRequest?.CropType?.Name,
            CreatedAt = match.CreatedAt,
            ContractId = match.Contract?.ContractId,
            ContractFullySigned = match.Contract?.Status == ContractStatus.Signed,
            CanMessage = match.Contract?.Status == ContractStatus.Signed
        });
    }

    public async Task<Result<List<FactoryMessageDto>>> GetMessagesAsync(Guid userId, Guid matchId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<List<FactoryMessageDto>>.Failure(FactoryErrors.FactoryNotFound);

        var match = await _factoryRepository.GetMatchForFactoryAsync(factory.FactoryId, matchId);
        if (match is null)
            return Result<List<FactoryMessageDto>>.Failure(FactoryErrors.ConversationNotFound);

        if (match.Contract is null || match.Contract.Status != ContractStatus.Signed)
            return Result<List<FactoryMessageDto>>.Failure(FactoryErrors.CannotSendMessage);

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

        if (match.Contract is null || match.Contract.Status != ContractStatus.Signed)
            return Result.Failure(FactoryErrors.CannotSendMessage);

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

                await _integrity.SupersedeActiveAsync(contract.ContractId);
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

        if (!MatchEligibilityGuard.IsStillEligible(match))
            return Result<FactoryContractDto>.Failure(FactoryErrors.EligibilityChanged);

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

        if (!ContractDealFunding.TryGetDealTotalEgp(match, out var dealTotalEgp))
            return Result<FactoryContractDto>.Failure(WalletErrors.DealValueInvalid);

        var holdAmount = _walletService.GetDealHoldAmountEgp(dealTotalEgp);

        // Always require factory liquidity before factory signs (even as first signer).
        var fundsOk = await _walletService.EnsureFactoryAvailableAsync(factory.FactoryId, holdAmount);
        if (fundsOk.IsFailure)
            return Result<FactoryContractDto>.Failure(fundsOk.Error!);

        contract.FactorySignedAt = DateTime.UtcNow;
        // FarmSignedAt must remain unchanged.
        contract.RefreshSignatureStatus();
        ContractExecution.AcceptMatchIfFullySigned(contract);

        if (contract.IsFullySigned && !contract.HasDealFundsHeld)
        {
            var hold = await _walletService.HoldDealFundsAsync(
                factory.FactoryId,
                contract.ContractId,
                holdAmount,
                $"Deal funds held on full signature for contract {contract.ContractId:N}");
            if (hold.IsFailure)
                return Result<FactoryContractDto>.Failure(hold.Error!);

            contract.FundsHeldAt = DateTime.UtcNow;
            contract.FundsHeldEgp = holdAmount;
        }

        if (contract.IsFullySigned)
            await _integrity.AnchorIfFullySignedAsync(contract);

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
                contract.FarmMatch?.SupplyRequest?.DeliveryDate,
                contract.FarmMatch?.SupplyRequest?.DeliveryPoint,
                contract.FarmMatch?.SupplyRequest?.FreightPayer,
                contract.FarmMatch?.SupplyRequest?.TransitRisk);
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

        if (contract.Status == ContractStatus.Cancelled)
            return Result<FactoryContractDto>.Failure(FactoryErrors.ContractNotPending);

        if (contract.Status == ContractStatus.Signed)
            return await UnwindSignedContractAsync(
                userId,
                factory.Name,
                contract,
                asFarm: false);

        contract.Status = ContractStatus.Cancelled;
        contract.ClearSignatures();
        ContractExecution.RejectMatchIfProposed(contract);
        await _integrity.SupersedeActiveAsync(contract.ContractId);
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

    private async Task<Result<FactoryContractDto>> UnwindSignedContractAsync(
        Guid userId,
        string actorName,
        Contract contract,
        bool asFarm)
    {
        var reason = asFarm
            ? "Signed contract cancelled by farm"
            : "Signed contract cancelled by factory";

        var unwind = await _escrowPayments.UnwindSignedDealAsync(contract.ContractId, userId, reason);
        if (unwind.IsFailure)
            return Result<FactoryContractDto>.Failure(unwind.Error!);

        contract.Status = ContractStatus.Cancelled;
        await _integrity.SupersedeActiveAsync(contract.ContractId);
        _contractRepository.Update(contract);

        var counterparty = asFarm
            ? contract.FarmMatch?.SupplyRequest?.Factory?.UserId
            : contract.FarmMatch?.Farm?.UserId;
        if (counterparty is Guid uid && uid != Guid.Empty && uid != userId)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = uid,
                Title = "Signed contract cancelled",
                Message = $"{actorName} cancelled the signed supply contract. Held funds were returned to the factory wallet.",
                Type = "ContractCancelled",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        var saved = await TrySaveFactoryContractAsync();
        if (saved.IsFailure)
            return Result<FactoryContractDto>.Failure(saved.Error!);

        await _fulfillmentService.VoidForContractAsync(contract.ContractId, userId, reason);
        return Result<FactoryContractDto>.Success(MapContract(contract));
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
        FarmUserId = c.FarmMatch?.Farm?.UserId,
        FactoryUserId = c.FarmMatch?.SupplyRequest?.Factory?.UserId,
        CanUnwindSigned = c.Status == ContractStatus.Signed
            && (c.Fulfillment is null
                || c.Fulfillment.Status is FulfillmentStatus.Planned or FulfillmentStatus.Shipped),
        UpdatedAt = c.SignedAt ?? c.FarmSignedAt ?? c.FactorySignedAt ?? c.CreatedAt,
        MatchScore = c.FarmMatch?.MatchScore,
        RiskScore = c.FarmMatch?.RiskScore,
        Integrity = ContractIntegrityService.MapActive(c)
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
