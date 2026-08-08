using NileChain.Application.Common;
using NileChain.Application.Dtos.Review;

namespace NileChain.Application.Interfaces;

public interface IReviewService
{
    Task<Result<ReviewDto>> CreateReviewAsync(Guid reviewerId, CreateReviewRequest request);
    Task<Result<List<ReviewDto>>> GetReviewsForTargetAsync(Guid targetId);
}
