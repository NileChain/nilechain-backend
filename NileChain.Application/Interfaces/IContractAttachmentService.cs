using Microsoft.AspNetCore.Http;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Contracts;
using NileChain.Domain.Enums;

namespace NileChain.Application.Interfaces;

public interface IContractAttachmentService
{
    Task<Result<List<ContractAttachmentDto>>> ListAsync(Guid userId, Guid contractId, bool isFactory);
    Task<Result<ContractAttachmentDto>> UploadAsync(
        Guid userId,
        Guid contractId,
        bool isFactory,
        IFormFile file,
        ContractAttachmentKind kind);
    Task<Result> DeleteAsync(Guid userId, Guid contractId, Guid attachmentId, bool isFactory);
}
