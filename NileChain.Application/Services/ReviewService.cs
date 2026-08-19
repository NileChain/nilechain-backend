using NileChain.Application.Common;
using NileChain.Application.Dtos.Review;
using NileChain.Application.Interfaces;
using NileChain.Application.Reviews;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IRepository<Review> _reviewRepository;
    private readonly IRepository<Contract> _contractRepository;
    private readonly IRepository<FarmMatch> _farmMatchRepository;
    private readonly IFarmRepository _farmRepository;
    private readonly IRepository<Factory> _factoryRepository;
    private readonly IRepository<SupplyRequest> _supplyRequestRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewService(
        IRepository<Review> reviewRepository,
        IRepository<Contract> contractRepository,
        IRepository<FarmMatch> farmMatchRepository,
        IFarmRepository farmRepository,
        IRepository<Factory> factoryRepository,
        IRepository<SupplyRequest> supplyRequestRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository;
        _contractRepository = contractRepository;
        _farmMatchRepository = farmMatchRepository;
        _farmRepository = farmRepository;
        _factoryRepository = factoryRepository;
        _supplyRequestRepository = supplyRequestRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<ReviewDto>> CreateReviewAsync(Guid reviewerId, CreateReviewRequest request)
    {
        if (request.Rating is < 1 or > 5)
            return Result<ReviewDto>.Failure(new Error("Review.InvalidRating", "Rating must be between 1 and 5."));

        var contracts = await _contractRepository.GetAllAsync();
        var contract = contracts.FirstOrDefault(c => c.ContractId == request.ContractId);
        if (contract is null)
            return Result<ReviewDto>.Failure(new Error("Review.ContractNotFound", "Contract not found."));

        if (contract.Status != ContractStatus.Signed)
            return Result<ReviewDto>.Failure(new Error("Review.ContractNotSigned", "Only signed contracts can be reviewed."));

        var (farmUserId, factoryUserId) = await ResolvePartyUserIdsAsync(contract);
        var partyCheck = ReviewPartyAuthorization.Validate(
            reviewerId,
            request.TargetId,
            farmUserId,
            factoryUserId);
        if (!partyCheck.IsSuccess)
            return Result<ReviewDto>.Failure(partyCheck.Error!);

        var existing = (await _reviewRepository.GetAllAsync())
            .Any(r => r.ContractId == request.ContractId && r.ReviewerId == reviewerId);
        if (existing)
            return Result<ReviewDto>.Failure(new Error("Review.AlreadyExists", "You already reviewed this contract."));

        var review = new Review
        {
            ReviewId = Guid.NewGuid(),
            ContractId = request.ContractId,
            ReviewerId = reviewerId,
            TargetId = request.TargetId,
            Rating = request.Rating,
            Comment = request.Comment,
            CreatedAt = DateTime.UtcNow
        };

        await _reviewRepository.AddAsync(review);
        await UpdateTargetRatingAsync(request.TargetId, review);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (UniqueConstraintViolation.IsViolation(ex))
        {
            return Result<ReviewDto>.Failure(new Error(
                "Review.AlreadyExists",
                "You already reviewed this contract."));
        }

        return Result<ReviewDto>.Success(Map(review, includeComment: true));
    }

    public async Task<Result<List<ReviewDto>>> GetReviewsForContractAsync(
        Guid contractId,
        Guid viewerId,
        bool isAdmin)
    {
        var reviews = (await _reviewRepository.GetAllAsync())
            .Where(r => r.ContractId == contractId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        var contract = (await _contractRepository.GetAllAsync())
            .FirstOrDefault(c => c.ContractId == contractId);
        (Guid? FarmUserId, Guid? FactoryUserId) parties = contract is null
            ? (null, null)
            : await ResolvePartyUserIdsAsync(contract);
        var showComments = isAdmin
            || viewerId == parties.FarmUserId
            || viewerId == parties.FactoryUserId;

        return Result<List<ReviewDto>>.Success(
            reviews.Select(r => Map(r, includeComment: showComments)).ToList());
    }

    public async Task<Result<List<ReviewDto>>> GetReviewsForTargetAsync(
        Guid targetId,
        Guid viewerId,
        bool isAdmin)
    {
        var reviews = (await _reviewRepository.GetAllAsync())
            .Where(r => r.TargetId == targetId)
            .OrderByDescending(r => r.CreatedAt)
            .ToList();

        if (isAdmin)
        {
            return Result<List<ReviewDto>>.Success(
                reviews.Select(r => Map(r, includeComment: true)).ToList());
        }

        var contractIds = reviews.Select(r => r.ContractId).Distinct().ToHashSet();
        var contracts = (await _contractRepository.GetAllAsync())
            .Where(c => contractIds.Contains(c.ContractId))
            .ToList();

        var canReadComment = new HashSet<Guid>();
        foreach (var contract in contracts)
        {
            var parties = await ResolvePartyUserIdsAsync(contract);
            if (viewerId == parties.FarmUserId || viewerId == parties.FactoryUserId)
                canReadComment.Add(contract.ContractId);
        }

        return Result<List<ReviewDto>>.Success(
            reviews.Select(r => Map(r, includeComment: canReadComment.Contains(r.ContractId))).ToList());
    }

    private async Task<(Guid? FarmUserId, Guid? FactoryUserId)> ResolvePartyUserIdsAsync(Contract contract)
    {
        if (contract.FarmMatch?.Farm is not null
            && contract.FarmMatch.SupplyRequest?.Factory is not null)
        {
            return (
                contract.FarmMatch.Farm.UserId,
                contract.FarmMatch.SupplyRequest.Factory.UserId);
        }

        var matches = await _farmMatchRepository.GetAllAsync();
        var match = matches.FirstOrDefault(m => m.MatchId == contract.MatchId);
        if (match is null)
            return (null, null);

        var farms = await _farmRepository.GetAllAsync();
        var farm = farms.FirstOrDefault(f => f.FarmId == match.FarmId);

        var requests = await _supplyRequestRepository.GetAllAsync();
        var request = requests.FirstOrDefault(r => r.RequestId == match.RequestId);
        if (request is null)
            return (farm?.UserId, null);

        var factories = await _factoryRepository.GetAllAsync();
        var factory = factories.FirstOrDefault(f => f.FactoryId == request.FactoryId);
        return (farm?.UserId, factory?.UserId);
    }

    /// <summary>
    /// <paramref name="pendingReview"/> is still unsaved, so it is not visible to the
    /// repository query and must be folded in explicitly — otherwise the stored average
    /// lags one review behind and the very first review leaves the target at zero.
    /// </summary>
    private async Task UpdateTargetRatingAsync(Guid targetId, Review pendingReview)
    {
        var reviews = (await _reviewRepository.GetAllAsync())
            .Where(r => r.TargetId == targetId && r.ReviewId != pendingReview.ReviewId)
            .Append(pendingReview)
            .ToList();

        var avg = Math.Round((decimal)reviews.Average(r => r.Rating), 2);
        var count = reviews.Count;

        var farms = await _farmRepository.GetAllAsync();
        var farm = farms.FirstOrDefault(f => f.UserId == targetId);
        if (farm is not null)
        {
            farm.AverageRating = avg;
            farm.RatingCount = count;
            _farmRepository.Update(farm);
            await RefreshFarmTrustScoreAsync(targetId, avg);
            return;
        }

        var factories = await _factoryRepository.GetAllAsync();
        var factory = factories.FirstOrDefault(f => f.UserId == targetId);
        if (factory is not null)
        {
            factory.AverageRating = avg;
            factory.RatingCount = count;
            _factoryRepository.Update(factory);
        }
    }

    /// <summary>
    /// Ratings are 20 of the 100 trust points, and matching ranks on the cached
    /// <c>Farm.RiskScore</c> column. Without this refresh a new review would not affect
    /// shortlists until something else happened to recompute the score.
    /// </summary>
    private async Task RefreshFarmTrustScoreAsync(Guid farmUserId, decimal averageRating)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(farmUserId);
        if (farm is null)
            return;

        var matchIds = (await _farmMatchRepository.GetAllAsync())
            .Where(m => m.FarmId == farm.FarmId)
            .Select(m => m.MatchId)
            .ToHashSet();

        var signedContracts = (await _contractRepository.GetAllAsync())
            .Count(c => c.Status == ContractStatus.Signed && matchIds.Contains(c.MatchId));

        var breakdown = FarmTrustScore.Compute(
            FarmTrustScore.InputsFrom(farm, signedContracts, averageRating, DateTime.UtcNow));

        if (farm.RiskScore == breakdown.Overall)
            return;

        farm.RiskScore = breakdown.Overall;
        _farmRepository.Update(farm);
    }

    private static ReviewDto Map(Review review, bool includeComment = true) => new()
    {
        ReviewId = review.ReviewId,
        ContractId = review.ContractId,
        ReviewerId = review.ReviewerId,
        TargetId = review.TargetId,
        Rating = review.Rating,
        Comment = includeComment ? review.Comment : null,
        CreatedAt = review.CreatedAt
    };
}
