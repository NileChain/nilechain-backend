using Microsoft.EntityFrameworkCore;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Infrastructure.Persistence.Repositories;

public sealed class SubscriptionRepository : ISubscriptionRepository
{
    private readonly NileChainDbContext _db;

    public SubscriptionRepository(NileChainDbContext db) => _db = db;

    public Task<Subscription?> GetLiveAsync(Guid userId, DateTime utcNow, CancellationToken cancellationToken = default) =>
        _db.Subscriptions
            .Where(s => s.UserId == userId
                        && s.Status == SubscriptionStatus.Active
                        && s.PeriodEnd > utcNow)
            .OrderByDescending(s => s.PeriodEnd)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<Subscription?> GetLatestAsync(Guid userId, CancellationToken cancellationToken = default) =>
        _db.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.PeriodEnd)
            .ThenByDescending(s => s.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, Subscription>> GetLatestForUsersAsync(
        IReadOnlyList<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, Subscription>();

        var rows = await _db.Subscriptions
            .Where(s => userIds.Contains(s.UserId))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(s => s.UserId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(s => s.PeriodEnd).ThenByDescending(s => s.UpdatedAt).First());
    }

    public Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default) =>
        _db.Subscriptions.AddAsync(subscription, cancellationToken).AsTask();

    public void Update(Subscription subscription) => _db.Subscriptions.Update(subscription);

    public Task<SubscriptionUsage?> GetUsageAsync(
        Guid userId,
        DateTime periodStart,
        SubscriptionMetric metric,
        CancellationToken cancellationToken = default) =>
        _db.SubscriptionUsages.FirstOrDefaultAsync(
            u => u.UserId == userId && u.PeriodStart == periodStart && u.Metric == metric,
            cancellationToken);

    public Task AddUsageAsync(SubscriptionUsage usage, CancellationToken cancellationToken = default) =>
        _db.SubscriptionUsages.AddAsync(usage, cancellationToken).AsTask();
}
