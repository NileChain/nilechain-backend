using NileChain.Infrastructure.Persistence;

namespace NileChain.API.Health;

public sealed class EfDatabasePing(NileChainDbContext db) : IDatabasePing
{
    public Task<bool> CanConnectAsync(CancellationToken cancellationToken = default) =>
        db.Database.CanConnectAsync(cancellationToken);
}
