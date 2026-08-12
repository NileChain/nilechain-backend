using NileChain.Application.Common;
using NileChain.Application.Dtos.Integrity;
using NileChain.Domain.Entities;

namespace NileChain.Application.Interfaces;

public interface IContractIntegrityService
{
    Task AnchorIfFullySignedAsync(Contract contract);
    Task SupersedeActiveAsync(Guid contractId);
    Task<Result<ContractIntegrityDto>> GetActiveForContractAsync(Guid contractId);
    Task<ContractIntegrityVerifyDto> VerifyByHashAsync(string contentHash);
}
