using Microsoft.AspNetCore.Http;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Dispute;

namespace NileChain.Application.Interfaces;

public interface IDisputeService
{
    Task<bool> HasActiveDisputeAsync(Guid contractId);

    Task<Result<DisputeDto>> OpenAsync(
        Guid userId,
        Guid contractId,
        bool asFarm,
        string type,
        string description,
        IReadOnlyList<IFormFile>? evidenceFiles);

    Task<Result<IReadOnlyList<DisputeDto>>> ListForContractAsync(
        Guid userId,
        Guid contractId,
        bool asFarm);

    Task<Result<DisputeDto>> GetAsync(Guid userId, Guid disputeId, bool asFarm);

    Task<Result<DisputeListDto>> ListAdminAsync(
        string? status,
        string? type,
        int page,
        int pageSize);

    Task<Result<DisputeDto>> GetAdminAsync(Guid disputeId);

    Task<Result<DisputeDto>> MoveToUnderReviewAsync(Guid adminUserId, Guid disputeId, string? adminNote);

    Task<Result<DisputeDto>> ResolveAsync(
        Guid adminUserId,
        Guid disputeId,
        string adminNote,
        string outcomeFavor);

    Task<Result<DisputeDto>> RejectAsync(Guid adminUserId, Guid disputeId, string adminNote);
}
