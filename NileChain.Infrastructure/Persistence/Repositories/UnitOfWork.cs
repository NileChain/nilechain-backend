using NileChain.Domain.Interfaces;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly NileChainDbContext _context;
    public UnitOfWork(NileChainDbContext context) => _context = context;
    public Task<int> SaveChangesAsync() => _context.SaveChangesAsync();
}
