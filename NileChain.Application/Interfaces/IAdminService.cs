using NileChain.Application.Common;
using NileChain.Application.Dtos.Admin;

namespace NileChain.Application.Interfaces
{
    public interface IAdminService
    {
        Task<PagedResult<UserListItem>> GetUsersAsync(string? role, bool? isVerified, string? search, int page, int pageSize);
        Task<UserListItem> CreateUserAsync(CreateUserRequest request);
        Task<UserListItem> UpdateUserAsync(Guid userId, UpdateUserRequest request);
        Task<Result<VerifyUserResult>> VerifyUserAsync(Guid userId);
        Task<Result<VerifyUserResult>> AnalyzeKybAsync(Guid userId);
        Task<Result<VerifyUserResult>> GetLastKybReportAsync(Guid userId);
        Task<Result> ApproveUserAsync(Guid adminUserId, Guid userId, string? reason);
        Task<Result> RequestKybInfoAsync(Guid adminUserId, Guid userId, string reason);
        Task<Result<FarmHygieneDto>> GetFarmHygieneAsync(Guid farmId);
        Task<Result<FactoryHygieneDto>> GetFactoryHygieneAsync(Guid factoryId);
        Task<Result<AdminOpsBadgesDto>> GetOpsBadgesAsync(CancellationToken cancellationToken = default);
        Task<Result> GrantFarmCertificationAsync(Guid adminUserId, Guid farmId, GrantFarmCertificationRequest request);
        Task<Result> RevokeFarmCertificationAsync(Guid farmId, Guid certificationId);
        Task<Result> BlockUserAsync(Guid userId);
        Task<Result> UnblockUserAsync(Guid userId);
        Task<Result> DeactivateUserAsync(Guid userId);
        Task<Result> ReactivateUserAsync(Guid userId);
        Task<Result> RejectUserAsync(Guid adminUserId, Guid userId, string reason);
        Task<Result> DeleteUserAsync(Guid userId);
        Task<Result<RagUploadResult>> UploadRagDocumentAsync(
            Guid uploadedBy,
            string title,
            string? category,
            string filePath,
            string contentText);
        Task<Result<List<RagDocumentDto>>> GetRagDocumentsAsync();
        Task<Result<DashboardSummaryDto>> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
        Task<Result<AdminContractListDto>> GetContractsAsync(
            string? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);
    }
}
