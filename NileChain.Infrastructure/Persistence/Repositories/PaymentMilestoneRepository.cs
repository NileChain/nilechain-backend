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
        bool requireNoActiveDispute = false,
        string? receiptUrl = null,
        string? receiptPublicId = null,
        string? receiptFileName = null)
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
            TransactionStatus.MarkedPaid when receiptUrl is not null => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.MarkedPaid)
                .SetProperty(t => t.PaidAt, utcNow)
                .SetProperty(t => t.ReceiptUrl, receiptUrl)
                .SetProperty(t => t.ReceiptPublicId, receiptPublicId)
                .SetProperty(t => t.ReceiptFileName, receiptFileName)
                .SetProperty(t => t.ReceiptUploadedAt, utcNow)),

            TransactionStatus.MarkedPaid => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.MarkedPaid)
                .SetProperty(t => t.PaidAt, utcNow)),

            TransactionStatus.EscrowHeld => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.EscrowHeld)
                .SetProperty(t => t.PaidAt, utcNow)),

            TransactionStatus.Completed => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.Completed)
                .SetProperty(t => t.ReceivedAt, utcNow)),

            TransactionStatus.Refunded => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.Refunded)),

            TransactionStatus.Voided => await query.ExecuteUpdateAsync(s => s
                .SetProperty(t => t.Status, TransactionStatus.Voided)
                .SetProperty(t => t.VoidedAt, utcNow)),

            _ => 0
        };

        return rows == 1;
    }

    public async Task<(Guid? TransactionId, decimal? PreviousAmount, decimal? NewAmount)> TryApplyDiscountToFirstOpenMilestoneAsync(
        Guid contractId,
        decimal discountPercent,
        Guid actorUserId,
        DateTime utcNow)
    {
        if (discountPercent <= 0 || discountPercent > 100)
            return (null, null, null);

        var generation = await _db.Transactions
            .Where(t => t.ContractId == contractId)
            .Select(t => (int?)t.ScheduleGeneration)
            .MaxAsync() ?? 0;

        var target = await _db.Transactions
            .Where(t =>
                t.ContractId == contractId
                && t.ScheduleGeneration == generation
                && t.Status != TransactionStatus.Voided
                && t.Status != TransactionStatus.Completed
                && t.Status != TransactionStatus.Refunded
                && (t.Status == TransactionStatus.Pending
                    || t.Status == TransactionStatus.MarkedPaid
                    || t.Status == TransactionStatus.EscrowHeld))
            .OrderBy(t => t.Sequence)
            .FirstOrDefaultAsync();

        if (target is null)
            return (null, null, null);

        var previous = target.Amount;
        var factor = 1m - (discountPercent / 100m);
        var next = decimal.Round(previous * factor, 2, MidpointRounding.AwayFromZero);
        if (next < 0) next = 0;
        if (next == previous)
            return (null, previous, next);

        target.Amount = next;
        _db.Transactions.Update(target);
        await _db.TransactionEvents.AddAsync(new TransactionEvent
        {
            EventId = Guid.NewGuid(),
            TransactionId = target.TransactionId,
            FromStatus = target.Status,
            ToStatus = target.Status,
            ActorUserId = actorUserId,
            Note = $"Amount adjusted by QC discount {discountPercent:0.##}% (was {previous}, now {next}).",
            CreatedAt = utcNow
        });

        return (target.TransactionId, previous, next);
    }

    public async Task<IReadOnlyList<(Guid TransactionId, decimal PreviousAmount, decimal NewAmount)>>
        TryScaleOpenMilestonesByFactorAsync(
            Guid contractId,
            decimal factor,
            Guid actorUserId,
            DateTime utcNow)
    {
        if (factor <= 0 || factor >= 1m)
            return Array.Empty<(Guid, decimal, decimal)>();

        var generation = await _db.Transactions
            .Where(t => t.ContractId == contractId)
            .Select(t => (int?)t.ScheduleGeneration)
            .MaxAsync() ?? 0;

        var targets = await _db.Transactions
            .Where(t =>
                t.ContractId == contractId
                && t.ScheduleGeneration == generation
                && (t.Status == TransactionStatus.Pending
                    || t.Status == TransactionStatus.MarkedPaid
                    || t.Status == TransactionStatus.EscrowHeld))
            .OrderBy(t => t.Sequence)
            .ToListAsync();

        var adjusted = new List<(Guid, decimal, decimal)>();
        foreach (var target in targets)
        {
            var previous = target.Amount;
            var next = decimal.Round(previous * factor, 2, MidpointRounding.AwayFromZero);
            if (next < 0) next = 0;
            if (next == previous)
                continue;

            target.Amount = next;
            _db.Transactions.Update(target);
            await _db.TransactionEvents.AddAsync(new TransactionEvent
            {
                EventId = Guid.NewGuid(),
                TransactionId = target.TransactionId,
                FromStatus = target.Status,
                ToStatus = target.Status,
                ActorUserId = actorUserId,
                Note = $"Amount scaled by weighbridge factor {factor:0.####} (was {previous}, now {next}).",
                CreatedAt = utcNow
            });
            adjusted.Add((target.TransactionId, previous, next));
        }

        return adjusted;
    }
}
