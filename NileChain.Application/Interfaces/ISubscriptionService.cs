using NileChain.Application.Common;
using NileChain.Application.Dtos.Billing;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;

namespace NileChain.Application.Interfaces;

public interface ISubscriptionService
{
    Task<Result<BillingMeDto>> GetMineAsync(Guid userId, bool asFarm, CancellationToken cancellationToken = default);
    Task<Result<BillingMeDto>> SubscribeAsync(Guid userId, bool asFarm, CancellationToken cancellationToken = default);
    Task<Result<BillingMeDto>> AdminGrantAsync(
        Guid userId,
        string planCode,
        DateTime? periodEndUtc,
        CancellationToken cancellationToken = default);

    /// <summary>Check quota before a metered action. Does not increment.</summary>
    Task<Result> EnsureCanConsumeAsync(
        Guid userId,
        SubscriptionMetric metric,
        CancellationToken cancellationToken = default);

    /// <summary>Increment usage after a successful metered action.</summary>
    Task ConsumeAsync(
        Guid userId,
        SubscriptionMetric metric,
        CancellationToken cancellationToken = default);

    Task<Result> EnsureFeatureAsync(
        Guid userId,
        SubscriptionFeatureFlag flag,
        CancellationToken cancellationToken = default);
}

public interface ISubscriptionRepository
{
    Task<Subscription?> GetLiveAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default);
    Task<Subscription?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, Subscription>> GetLatestForUsersAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default);
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);
    void Update(Subscription subscription);
    Task<SubscriptionUsage?> GetUsageAsync(
        Guid userId,
        DateTime periodStart,
        SubscriptionMetric metric,
        CancellationToken cancellationToken = default);
    Task AddUsageAsync(SubscriptionUsage usage, CancellationToken cancellationToken = default);
}
