using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces;

public interface IFarmRepository : IRepository<Farm>
{
    Task<Farm?> GetByUserIdAsync(Guid userId);
    Task<Farm?> GetFarmWithDetailsAsync(Guid userId);
    Task<IReadOnlyList<Farm>> GetVerifiedFarmsByCropAsync(Guid cropTypeId, string? governorate);
}
