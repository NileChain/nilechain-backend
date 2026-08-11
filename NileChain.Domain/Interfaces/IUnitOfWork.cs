namespace NileChain.Domain.Interfaces;

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync();

    /// <summary>Begins a DB transaction; caller must Commit or dispose (rollback).</summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
}
