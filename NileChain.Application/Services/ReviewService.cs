using NileChain.Application.Common;
using NileChain.Application.Dtos.Review;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class ReviewService : IReviewService
{
    private readonly IRepository<Review> _reviewRepository;
    private readonly IRepository<Contract> _contractRepository;
    private readonly IRepository<Farm> _farmRepository;
    private readonly IRepository<Factory> _factoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReviewService(
        IRepository<Review> reviewRepository,
        IRepository<Contract> contractRepository,
        IRepository<Farm> farmRepository,
        IRepository<Factory> factoryRepository,
        IUnitOfWork unitOfWork)
    {
        _reviewRepository = reviewRepository;
        _contractRepository = contractRepository;
        _farmRepository = farmRepository;
        _factoryRepository = factoryRepository;
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
        await UpdateTargetRatingAsync(request.TargetId);
        await _unitOfWork.SaveChangesAsync();

        return Result<ReviewDto>.Success(Map(review));
    }

    public async Task<Result<List<ReviewDto>>> GetReviewsForTargetAsync(Guid targetId)
    {
        var reviews = (await _reviewRepository.GetAllAsync())
            .Where(r => r.TargetId == targetId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(Map)
            .ToList();

        return Result<List<ReviewDto>>.Success(reviews);
    }

    private async Task UpdateTargetRatingAsync(Guid targetId)
    {
        var reviews = (await _reviewRepository.GetAllAsync())
            .Where(r => r.TargetId == targetId)
            .ToList();

        if (reviews.Count == 0)
            return;

        var avg = Math.Round((decimal)reviews.Average(r => r.Rating), 2);
        var count = reviews.Count;

        var farms = await _farmRepository.GetAllAsync();
        var farm = farms.FirstOrDefault(f => f.UserId == targetId);
        if (farm is not null)
        {
            farm.AverageRating = avg;
            farm.RatingCount = count;
            _farmRepository.Update(farm);
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

    private static ReviewDto Map(Review r) => new()
    {
        ReviewId = r.ReviewId,
        ContractId = r.ContractId,
        ReviewerId = r.ReviewerId,
        TargetId = r.TargetId,
        Rating = r.Rating,
        Comment = r.Comment,
        CreatedAt = r.CreatedAt
    };
}
