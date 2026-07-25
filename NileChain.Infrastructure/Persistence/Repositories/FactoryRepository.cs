using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class FactoryRepository : Repository<Factory>, IFactoryRepository
{
    public FactoryRepository(NileChainDbContext context) : base(context) { }

    public async Task<Factory?> GetByUserIdAsync(Guid userId) =>
        await Context.Factory.FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<Factory?> GetFactoryWithDetailsAsync(Guid userId) =>
        await Context.Factory
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.UserId == userId);
}
