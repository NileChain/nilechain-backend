using Microsoft.EntityFrameworkCore;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class FulfillmentRepository : IFulfillmentRepository
{
    private readonly NileChainDbContext _db;

    public FulfillmentRepository(NileChainDbContext db) => _db = db;

    public Task<Fulfillment?> GetByContractIdAsync(Guid contractId, bool includeEvents = true)
    {
        IQueryable<Fulfillment> q = _db.Fulfillments
            .AsNoTracking()
            .Include(f => f.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.SupplyRequest)
                        .ThenInclude(r => r.Factory)
            .Include(f => f.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.Farm);

        if (includeEvents)
            q = q.Include(f => f.Events.OrderBy(e => e.CreatedAt));

        return q.FirstOrDefaultAsync(f => f.ContractId == contractId);
    }

    public Task<Fulfillment?> GetByIdAsync(Guid fulfillmentId) =>
        _db.Fulfillments.AsNoTracking().FirstOrDefaultAsync(f => f.FulfillmentId == fulfillmentId);

    public async Task AddAsync(Fulfillment fulfillment) =>
        await _db.Fulfillments.AddAsync(fulfillment);

    public async Task AddEventAsync(FulfillmentEvent fulfillmentEvent) =>
        await _db.FulfillmentEvents.AddAsync(fulfillmentEvent);

    public async Task<bool> TryAtomicTransitionAsync(
        Guid fulfillmentId,
        FulfillmentStatus expectedFrom,
        FulfillmentStatus to,
        DateTime utcNow,
        string? qualityNotes = null,
        bool requireNoActiveDispute = false)
    {
        var query = _db.Fulfillments.Where(f =>
            f.FulfillmentId == fulfillmentId && f.Status == expectedFrom);

        if (requireNoActiveDispute)
        {
            query = query.Where(f => !_db.Disputes.Any(d =>
                d.ContractId == f.ContractId
                && (d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview)));
        }

        var rows = to switch
        {
            FulfillmentStatus.Shipped => await query.ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FulfillmentStatus.Shipped)
                .SetProperty(f => f.ShippedAt, utcNow)),

            FulfillmentStatus.Received => await query.ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FulfillmentStatus.Received)
                .SetProperty(f => f.ReceivedAt, utcNow)),

            FulfillmentStatus.QualityChecked => await query.ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FulfillmentStatus.QualityChecked)
                .SetProperty(f => f.QualityCheckedAt, utcNow)
                .SetProperty(f => f.QualityNotes, qualityNotes)),

            FulfillmentStatus.Fulfilled => await query.ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FulfillmentStatus.Fulfilled)
                .SetProperty(f => f.FulfilledAt, utcNow)),

            FulfillmentStatus.Voided => await query.ExecuteUpdateAsync(s => s
                .SetProperty(f => f.Status, FulfillmentStatus.Voided)
                .SetProperty(f => f.VoidedAt, utcNow)),

            _ => 0
        };

        return rows == 1;
    }

    public async Task<IReadOnlyList<Fulfillment>> GetStuckPlannedAsync(DateTime asOfUtcNoon, int skip, int take)
    {
        return await StuckQuery(asOfUtcNoon)
            .OrderBy(f => f.PlannedShipDate)
            .Skip(skip)
            .Take(take)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<int> CountStuckPlannedAsync(DateTime asOfUtcNoon) =>
        StuckQuery(asOfUtcNoon).CountAsync();

    private IQueryable<Fulfillment> StuckQuery(DateTime asOfUtcNoon) =>
        _db.Fulfillments
            .Include(f => f.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.SupplyRequest)
                        .ThenInclude(r => r.Factory)
            .Include(f => f.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.Farm)
            .Where(f =>
                f.Status == FulfillmentStatus.Planned
                && f.PlannedShipDate != null
                && f.PlannedShipDate < asOfUtcNoon
                && f.Contract.Status == ContractStatus.Signed);
}
