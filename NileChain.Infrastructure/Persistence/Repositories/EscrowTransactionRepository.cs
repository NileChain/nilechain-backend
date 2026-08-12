using Microsoft.EntityFrameworkCore;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class EscrowTransactionRepository : IEscrowTransactionRepository
{
    private readonly NileChainDbContext _db;

    public EscrowTransactionRepository(NileChainDbContext db) => _db = db;

    public Task<EscrowTransaction?> GetByIdAsync(Guid escrowTransactionId, bool tracking = false)
    {
        IQueryable<EscrowTransaction> q = tracking ? _db.EscrowTransactions : _db.EscrowTransactions.AsNoTracking();
        return q.FirstOrDefaultAsync(e => e.EscrowTransactionId == escrowTransactionId);
    }

    public Task<EscrowTransaction?> GetActiveByTransactionIdAsync(Guid transactionId) =>
        _db.EscrowTransactions.AsNoTracking()
            .Where(e => e.TransactionId == transactionId
                        && (e.Status == EscrowStatus.Created
                            || e.Status == EscrowStatus.Pending
                            || e.Status == EscrowStatus.Held))
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync();

    public async Task<IReadOnlyList<EscrowTransaction>> GetByContractIdAsync(Guid contractId) =>
        await _db.EscrowTransactions.AsNoTracking()
            .Where(e => e.ContractId == contractId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

    public async Task AddAsync(EscrowTransaction escrow) =>
        await _db.EscrowTransactions.AddAsync(escrow);

    public Task UpdateAsync(EscrowTransaction escrow)
    {
        _db.EscrowTransactions.Update(escrow);
        return Task.CompletedTask;
    }

    public async Task<bool> TryAtomicStatusAsync(
        Guid escrowTransactionId,
        EscrowStatus expectedFrom,
        EscrowStatus to,
        DateTime utcNow,
        string? reason = null)
    {
        var query = _db.EscrowTransactions.Where(e =>
            e.EscrowTransactionId == escrowTransactionId && e.Status == expectedFrom);

        var rows = to switch
        {
            EscrowStatus.Held => await query.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EscrowStatus.Held)
                .SetProperty(e => e.HeldAt, utcNow)
                .SetProperty(e => e.UpdatedAt, utcNow)),

            EscrowStatus.Released => await query.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EscrowStatus.Released)
                .SetProperty(e => e.ReleasedAt, utcNow)
                .SetProperty(e => e.ReleaseReason, reason)
                .SetProperty(e => e.UpdatedAt, utcNow)),

            EscrowStatus.Refunded => await query.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EscrowStatus.Refunded)
                .SetProperty(e => e.RefundedAt, utcNow)
                .SetProperty(e => e.RefundReason, reason)
                .SetProperty(e => e.UpdatedAt, utcNow)),

            EscrowStatus.Failed => await query.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EscrowStatus.Failed)
                .SetProperty(e => e.FailedAt, utcNow)
                .SetProperty(e => e.FailReason, reason)
                .SetProperty(e => e.UpdatedAt, utcNow)),

            EscrowStatus.Pending => await query.ExecuteUpdateAsync(s => s
                .SetProperty(e => e.Status, EscrowStatus.Pending)
                .SetProperty(e => e.UpdatedAt, utcNow)),

            _ => 0
        };

        return rows == 1;
    }
}
