using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

/// <summary>Access-quota plan for a farm or factory user. Not a second GMV take-rate.</summary>
public class Subscription
{
    public Guid SubscriptionId { get; set; }
    public Guid UserId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Active;
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public SubscriptionSource Source { get; set; } = SubscriptionSource.Wallet;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }

    public bool IsLiveAt(DateTime utcNow) =>
        Status == SubscriptionStatus.Active && PeriodEnd > utcNow;
}

/// <summary>Per-period usage against a subscription meter.</summary>
public class SubscriptionUsage
{
    public Guid UsageId { get; set; }
    public Guid UserId { get; set; }
    public DateTime PeriodStart { get; set; }
    public SubscriptionMetric Metric { get; set; }
    public int Count { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ApplicationUser? User { get; set; }
}
