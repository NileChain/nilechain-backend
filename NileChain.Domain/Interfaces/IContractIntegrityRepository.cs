using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces;

public interface IContractIntegrityRepository
{
    Task<ContractIntegrityAnchor?> GetChainHeadAsync();
    Task<ContractIntegrityAnchor?> GetByContentHashAsync(string contentHash);
    Task<ContractIntegrityAnchor?> GetActiveForContractAsync(Guid contractId);
    Task<List<ContractIntegrityAnchor>> GetActiveAnchorsForContractAsync(Guid contractId);
    Task AddAsync(ContractIntegrityAnchor anchor);
    void Update(ContractIntegrityAnchor anchor);
}
