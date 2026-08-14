using NileChain.Application.Common;
using NileChain.Application.Dtos.Contracts;

namespace NileChain.Application.Interfaces;

public interface IContractDateAmendmentService
{
    Task<Result<ContractDateAmendmentDto>> ProposeAsync(
        Guid userId,
        Guid contractId,
        bool asFactory,
        ProposeContractDateAmendmentRequest request);

    Task<Result<ContractDateAmendmentDto>> AcceptAsync(
        Guid userId,
        Guid contractId,
        bool asFactory);

    Task<Result<ContractDateAmendmentDto>> RejectAsync(
        Guid userId,
        Guid contractId,
        bool asFactory);
}
