using NileChain.Application.Common;
using NileChain.Application.Dtos.Contracts;

namespace NileChain.Application.Interfaces;

public interface IContractChangeRequestService
{
    Task<Result<RequestContractChangesResponse>> RequestChangesAsync(
        Guid userId,
        Guid contractId,
        bool asFactory,
        RequestContractChangesRequest request,
        CancellationToken cancellationToken = default);
}
