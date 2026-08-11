using NileChain.Application.Common;

namespace NileChain.Application.Reviews;

/// <summary>
/// Ensures only contract parties may review, and TargetId is the counterparty.
/// </summary>
public static class ReviewPartyAuthorization
{
    public static Result Validate(
        Guid reviewerId,
        Guid targetId,
        Guid? farmUserId,
        Guid? factoryUserId)
    {
        if (farmUserId is null || factoryUserId is null
            || farmUserId == Guid.Empty || factoryUserId == Guid.Empty)
        {
            return Result.Failure(new Error(
                "Review.PartiesUnknown",
                "Could not resolve contract parties for this review."));
        }

        var farm = farmUserId.Value;
        var factory = factoryUserId.Value;

        if (reviewerId != farm && reviewerId != factory)
        {
            return Result.Failure(new Error(
                "Review.NotAParty",
                "Only parties to the contract may submit a review."));
        }

        var expectedTarget = reviewerId == farm ? factory : farm;
        if (targetId != expectedTarget)
        {
            return Result.Failure(new Error(
                "Review.InvalidTarget",
                "Review target must be the other party on the contract."));
        }

        return Result.Success();
    }
}
