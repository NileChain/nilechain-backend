using NileChain.Application.Common;
using NileChain.Application.Dtos.Review;

namespace NileChain.Application.Interfaces;

public interface IReviewService
{
    Task<Result<ReviewDto>> CreateReviewAsync(Guid reviewerId, CreateReviewRequest request);
    Task<Result<List<ReviewDto>>> GetReviewsForTargetAsync(Guid targetId, Guid viewerId, bool isAdmin);
    Task<Result<List<ReviewDto>>> GetReviewsForContractAsync(Guid contractId, Guid viewerId, bool isAdmin);
}
