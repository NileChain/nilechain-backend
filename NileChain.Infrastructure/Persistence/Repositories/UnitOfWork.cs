using Microsoft.EntityFrameworkCore.Storage;
using NileChain.Domain.Interfaces;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NileChainDbContext _context;

    public UnitOfWork(NileChainDbContext context) => _context = context;

    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        var tx = await _context.Database.BeginTransactionAsync(cancellationToken);
        return new EfUnitOfWorkTransaction(tx);
    }

    private sealed class EfUnitOfWorkTransaction(IDbContextTransaction tx) : IUnitOfWorkTransaction
    {
        public Task CommitAsync(CancellationToken cancellationToken = default) =>
            tx.CommitAsync(cancellationToken);

        public ValueTask DisposeAsync() => tx.DisposeAsync();
    }
}
