using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces;

public interface IFarmRepository : IRepository<Farm>
{
    Task<Farm?> GetByUserIdAsync(Guid userId);
    Task<Farm?> GetFarmWithDetailsAsync(Guid userId);
    Task<Farm?> GetFarmWithDashboardDataAsync(Guid userId);
    Task<IReadOnlyList<Farm>> GetVerifiedFarmsByCropAsync(Guid cropTypeId, string? governorate);
    Task<List<FarmMatch>> GetFarmMatchesAsync(Guid userId, string? status, Guid? cropTypeId, string? sort = null);
    Task<(List<FarmMatch> Items, int TotalCount)> GetFarmMatchesPageAsync(
        Guid userId,
        string? status,
        Guid? cropTypeId,
        string? sort,
        string? search,
        int? maxAgeDays,
        int page,
        int pageSize);
    Task<(int Total, int Proposed, int Accepted, int Rejected, int NewCount)> GetFarmMatchCountsAsync(
        Guid userId,
        DateTime newSinceUtc);
    Task<List<FarmMatch>> GetNewFarmMatchesAsync(Guid userId, DateTime newSinceUtc, int take);
    Task<FarmMatch?> GetFarmMatchByIdAsync(Guid userId, Guid matchId);
    Task<List<Contract>> GetFarmContractsAsync(Guid userId);
    Task<Contract?> GetContractForFarmAsync(Guid userId, Guid contractId);
    Task<Contract?> GetContractByMatchForFarmAsync(Guid userId, Guid matchId);
    Task<List<FarmMatch>> GetConversationsAsync(Guid userId);
    Task<List<Message>> GetMessagesAsync(Guid userId, Guid matchId);
    Task<List<Notification>> GetNotificationsAsync(Guid userId);
}
