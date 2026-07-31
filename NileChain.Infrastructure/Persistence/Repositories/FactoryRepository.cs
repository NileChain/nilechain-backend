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

    public async Task<SupplyRequest?> GetSupplyRequestByIdAsync(Guid requestId) =>
        await Context.SupplyRequests.FirstOrDefaultAsync(sr => sr.RequestId == requestId);

    public async Task<List<FarmMatch>> GetMatchesByRequestIdAsync(Guid factoryId, Guid requestId) =>
        await Context.FarmMatches
            .Include(fm => fm.Farm)
            .Where(fm => fm.RequestId == requestId && fm.SupplyRequest.FactoryId == factoryId)
            .OrderByDescending(fm => fm.MatchScore)
            .ToListAsync();
}
