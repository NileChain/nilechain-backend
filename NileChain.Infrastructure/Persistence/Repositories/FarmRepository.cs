using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class FarmRepository : Repository<Farm>, IFarmRepository
{
    public FarmRepository(NileChainDbContext context) : base(context) { }

    public async Task<Farm?> GetByUserIdAsync(Guid userId) =>
        await Context.Farm.FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<Farm?> GetFarmWithDetailsAsync(Guid userId) =>
        await Context.Farm
            .Include(f => f.User)
            .Include(f => f.CropTypes)
            .Include(f => f.FarmDocuments)
            .FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<IReadOnlyList<Farm>> GetVerifiedFarmsByCropAsync(Guid cropTypeId, string? governorate) =>
        await Context.Farm
            .Where(f => f.IsVerified && f.CropTypes.Any(c => c.CropTypeId == cropTypeId))
            .Where(f => governorate == null || f.Governorate == governorate)
            .ToListAsync();
}
