using Microsoft.EntityFrameworkCore;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class PaymentMilestoneRepository : IPaymentMilestoneRepository
{
    private readonly NileChainDbContext _db;

    public PaymentMilestoneRepository(NileChainDbContext db) => _db = db;

    public async Task<IReadOnlyList<Transaction>> GetByContractIdAsync(
        Guid contractId,
        bool includeEvents = true)
    {
        IQueryable<Transaction> q = _db.Transactions
            .AsNoTracking()
            .Where(t => t.ContractId == contractId);

        if (includeEvents)
            q = q.Include(t => t.Events.OrderBy(e => e.CreatedAt));

        return await q
            .OrderByDescending(t => t.ScheduleGeneration)
            .ThenBy(t => t.Sequence)
            .ToListAsync();
    }

    public Task<Transaction?> GetByIdAsync(Guid transactionId, bool includeEvents = true)
    {
        IQueryable<Transaction> q = _db.Transactions.AsNoTracking();
        if (includeEvents)
            q = q.Include(t => t.Events.OrderBy(e => e.CreatedAt));

        return q.FirstOrDefaultAsync(t => t.TransactionId == transactionId);
    }

    public Task<bool> HasActiveScheduleAsync(Guid contractId) =>
        _db.Transactions.AsNoTracking().AnyAsync(t =>
            t.ContractId == contractId && t.Status != TransactionStatus.Voided);

    public async Task<int> GetMaxScheduleGenerationAsync(Guid contractId)
    {
        var max = await _db.Transactions.AsNoTracking()
            .Where(t => t.ContractId == contractId)
            .Select(t => (int?)t.ScheduleGeneration)
            .MaxAsync();
        return max ?? 0;
    }

    public async Task AddRangeAsync(IEnumerable<Transaction> milestones)
    {
        await _db.Transactions.AddRangeAsync(milestones);
    }

    public async Task AddEventAsync(TransactionEvent transactionEvent) =>
        await _db.TransactionEvents.AddAsync(transactionEvent);

    public async Task AddEventsAsync(IEnumerable<TransactionEvent> events) =>
        await _db.TransactionEvents.AddRangeAsync(events);

    public async Task<bool> TryAtomicTransitionAsync(
        Guid transactionId,
        TransactionStatus expectedFrom,
        TransactionStatus to,
        DateTime utcNow,
        bool requireNoActiveDispute = false)
    {
        var query = _db.Transactions.Where(t =>
            t.TransactionId == transactionId && t.Status == expectedFrom);

        if (requireNoActiveDispute)
        {
            query = query.Where(t => !_db.Disputes.Any(d =>
                d.ContractId == t.ContractId
                && (d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview)));
        }

        var rows = to switch
        {
            TransactionStatus.MarkedPaid => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.MarkedPaid)
                .SetProperty(t => t.PaidAt, utcNow)),

            TransactionStatus.Completed => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.Completed)
                .SetProperty(t => t.ReceivedAt, utcNow)),

            TransactionStatus.Voided => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.Voided)
                .SetProperty(t => t.VoidedAt, utcNow)),

            _ => 0
        };

        return rows == 1;
    }
}
