using NileChain.Application.Common;
using NileChain.Application.Dtos.Admin;

namespace NileChain.Application.Interfaces
{
    public interface IAdminService
    {
        Task<PagedResult<UserListItem>> GetUsersAsync(string? role, bool? isVerified, string? search, int page, int pageSize);
        Task<UserListItem> CreateUserAsync(CreateUserRequest request);
        Task<UserListItem> UpdateUserAsync(Guid userId, UpdateUserRequest request);
        Task<Result> VerifyUserAsync(Guid userId);
        Task<Result> BlockUserAsync(Guid userId);
        Task<Result> UnblockUserAsync(Guid userId);
        Task<Result> DeactivateUserAsync(Guid userId);
        Task<Result> ReactivateUserAsync(Guid userId);
    }
}
