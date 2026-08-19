namespace NileChain.Domain.Enums;

public enum KybReviewStatus
{
    Pending = 0,
    RequestInfo = 1,
    Rejected = 2,
    Approved = 3
}

public enum KybRecommendation
{
    NeedsReview = 0,
    Approve = 1,
    Reject = 2
}

public enum KybDecisionAction
{
    Approved = 0,
    RequestInfo = 1,
    Rejected = 2
}
