using Microsoft.EntityFrameworkCore;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class DisputeRepository : IDisputeRepository
{
    private readonly NileChainDbContext _db;

    public DisputeRepository(NileChainDbContext db) => _db = db;

    public Task<Dispute?> GetByIdAsync(
        Guid disputeId,
        bool includeEvidence = true,
        bool includeEvents = true)
    {
        IQueryable<Dispute> q = DetailQuery();

        if (includeEvidence)
            q = q.Include(d => d.Evidence.OrderBy(e => e.UploadedAt));

        if (includeEvents)
            q = q.Include(d => d.Events.OrderBy(e => e.CreatedAt));

        return q.AsNoTracking().FirstOrDefaultAsync(d => d.DisputeId == disputeId);
    }

    public async Task<IReadOnlyList<Dispute>> GetByContractIdAsync(
        Guid contractId,
        bool includeEvidence = true,
        bool includeEvents = true)
    {
        IQueryable<Dispute> q = DetailQuery().Where(d => d.ContractId == contractId);

        if (includeEvidence)
            q = q.Include(d => d.Evidence.OrderBy(e => e.UploadedAt));

        if (includeEvents)
            q = q.Include(d => d.Events.OrderBy(e => e.CreatedAt));

        return await q
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .ToListAsync();
    }

    public Task<bool> HasActiveDisputeAsync(Guid contractId) =>
        _db.Disputes.AsNoTracking().AnyAsync(d =>
            d.ContractId == contractId
            && (d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview));

    public async Task AddAsync(Dispute dispute) =>
        await _db.Disputes.AddAsync(dispute);

    public async Task AddEvidenceAsync(DisputeEvidence evidence) =>
        await _db.DisputeEvidence.AddAsync(evidence);

    public async Task AddEventAsync(DisputeEvent disputeEvent) =>
        await _db.DisputeEvents.AddAsync(disputeEvent);

    public async Task<bool> TryAtomicTransitionAsync(
        Guid disputeId,
        DisputeStatus expectedFrom,
        DisputeStatus to,
        DateTime utcNow,
        string? adminNote,
        DisputeOutcomeFavor outcomeFavor,
        Guid actorUserId)
    {
        var query = _db.Disputes.Where(d =>
            d.DisputeId == disputeId && d.Status == expectedFrom);

        var rows = to switch
        {
            DisputeStatus.UnderReview => await query.ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DisputeStatus.UnderReview)
                .SetProperty(d => d.UnderReviewAt, utcNow)
                .SetProperty(d => d.ReviewedByUserId, actorUserId)
                .SetProperty(d => d.AdminNote, adminNote)),

            DisputeStatus.Resolved => await query.ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DisputeStatus.Resolved)
                .SetProperty(d => d.ResolvedAt, utcNow)
                .SetProperty(d => d.ResolvedByUserId, actorUserId)
                .SetProperty(d => d.AdminNote, adminNote)
                .SetProperty(d => d.OutcomeFavor, outcomeFavor)),

            DisputeStatus.Rejected => await query.ExecuteUpdateAsync(s => s
                .SetProperty(d => d.Status, DisputeStatus.Rejected)
                .SetProperty(d => d.RejectedAt, utcNow)
                .SetProperty(d => d.ResolvedByUserId, actorUserId)
                .SetProperty(d => d.AdminNote, adminNote)
                .SetProperty(d => d.OutcomeFavor, DisputeOutcomeFavor.None)),

            _ => 0
        };

        return rows == 1;
    }

    public async Task<(IReadOnlyList<Dispute> Items, int TotalCount)> ListAdminAsync(
        DisputeStatus? status,
        DisputeType? type,
        int skip,
        int take)
    {
        IQueryable<Dispute> q = DetailQuery();

        if (status.HasValue)
            q = q.Where(d => d.Status == status.Value);

        if (type.HasValue)
            q = q.Where(d => d.Type == type.Value);

        var total = await q.CountAsync();
        var items = await q
            .AsNoTracking()
            .Include(d => d.Evidence)
            .OrderByDescending(d => d.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync();

        return (items, total);
    }

    private IQueryable<Dispute> DetailQuery() =>
        _db.Disputes
            .Include(d => d.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.Farm)
            .Include(d => d.Contract)
                .ThenInclude(c => c.FarmMatch)
                    .ThenInclude(m => m.SupplyRequest)
                        .ThenInclude(r => r.Factory);
}
