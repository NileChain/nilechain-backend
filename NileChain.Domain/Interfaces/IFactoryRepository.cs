using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces;

public interface IFactoryRepository : IRepository<Factory>
{
    Task<Factory?> GetByUserIdAsync(Guid userId);
    Task<Factory?> GetFactoryWithDetailsAsync(Guid userId);
    Task<SupplyRequest?> GetSupplyRequestByIdAsync(Guid requestId);
    Task<List<FarmMatch>> GetMatchesByRequestIdAsync(Guid factoryId, Guid requestId);
}
