using Microsoft.EntityFrameworkCore;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class WalletRepository : IWalletRepository
{
    private readonly NileChainDbContext _db;

    public WalletRepository(NileChainDbContext db) => _db = db;

    public Task<Wallet?> GetByOwnerAsync(WalletOwnerType ownerType, Guid ownerId, bool tracking = true)
    {
        var q = _db.Wallets.Where(w => w.OwnerType == ownerType && w.OwnerId == ownerId);
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync();
    }

    public Task<Wallet?> GetByIdAsync(Guid walletId, bool tracking = true)
    {
        var q = _db.Wallets.AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(w => w.WalletId == walletId);
    }

    public Task AddAsync(Wallet wallet) => _db.Wallets.AddAsync(wallet).AsTask();

    public Task AddLedgerAsync(WalletLedgerEntry entry) =>
        _db.WalletLedgerEntries.AddAsync(entry).AsTask();

    public Task AddTopUpAsync(WalletTopUp topUp) => _db.WalletTopUps.AddAsync(topUp).AsTask();

    public Task<WalletTopUp?> GetTopUpByIdAsync(Guid topUpId, bool tracking = true)
    {
        var q = _db.WalletTopUps.AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(t => t.TopUpId == topUpId);
    }

    public Task<WalletTopUp?> GetTopUpByIdempotencyAsync(string idempotencyKey, bool tracking = true)
    {
        var q = _db.WalletTopUps.Where(t => t.IdempotencyKey == idempotencyKey);
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync();
    }

    public Task AddWithdrawalAsync(WalletWithdrawal withdrawal) =>
        _db.WalletWithdrawals.AddAsync(withdrawal).AsTask();

    public async Task<IReadOnlyList<WalletLedgerEntry>> GetRecentLedgerAsync(Guid walletId, int take = 30) =>
        await _db.WalletLedgerEntries.AsNoTracking()
            .Where(e => e.WalletId == walletId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .ToListAsync();

    public async Task<IReadOnlyList<WalletWithdrawal>> GetRecentWithdrawalsAsync(Guid walletId, int take = 20) =>
        await _db.WalletWithdrawals.AsNoTracking()
            .Where(w => w.WalletId == walletId)
            .OrderByDescending(w => w.CreatedAt)
            .Take(take)
            .ToListAsync();

    public Task<WalletWithdrawal?> GetWithdrawalByIdAsync(Guid withdrawalId, bool tracking = true)
    {
        var q = _db.WalletWithdrawals.AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(w => w.WithdrawalId == withdrawalId);
    }

    public async Task<IReadOnlyList<WalletWithdrawal>> ListWithdrawalsAsync(
        WalletWithdrawalStatus? status,
        int take = 100)
    {
        var q = _db.WalletWithdrawals.Include(w => w.Wallet).AsNoTracking().AsQueryable();
        if (status.HasValue)
            q = q.Where(w => w.Status == status.Value);
        return await q.OrderBy(w => w.CreatedAt).Take(take).ToListAsync();
    }

    public async Task<IReadOnlyList<WalletTopUp>> GetStalePendingTopUpsAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default) =>
        await _db.WalletTopUps
            .Where(t =>
                (t.Status == WalletTopUpStatus.Created || t.Status == WalletTopUpStatus.Pending)
                && t.CreatedAt <= cutoffUtc)
            .OrderBy(t => t.CreatedAt)
            .Take(200)
            .ToListAsync(cancellationToken);
}
