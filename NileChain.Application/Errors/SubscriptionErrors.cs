using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class SubscriptionErrors
{
    public static readonly Error QuotaExceeded =
        new(
            "Subscription.QuotaExceeded",
            "This month's free quota is used. Upgrade to Pro from Billing, then try again.");

    public static readonly Error PlanRequired =
        new(
            "Subscription.PlanRequired",
            "This action needs a Pro plan. Upgrade from Billing, then try again.");

    public static readonly Error NotApplicable =
        new(
            "Subscription.NotApplicable",
            "Marketplace subscriptions apply to farm and factory accounts only.");

    public static readonly Error InvalidPlan =
        new("Subscription.InvalidPlan", "Unknown subscription plan.");

    public static readonly Error UserNotFound =
        new("Subscription.UserNotFound", "User was not found.");
}
