using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces;

public interface IContractSignatureRepository
{
    Task AddSignatureAsync(ContractSignatureRecord record);
    Task AddAuditAsync(ContractAuditLog log);
    Task<ContractAuditLog?> GetLatestAuditAsync(Guid contractId);
    Task<List<ContractSignatureRecord>> GetSignaturesForContractAsync(Guid contractId);
    Task<List<ContractAuditLog>> GetAuditTrailAsync(Guid contractId);
    Task<Contract?> GetContractWithPartiesAsync(Guid contractId);
}
