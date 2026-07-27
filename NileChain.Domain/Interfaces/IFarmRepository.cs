using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces;

public interface IFarmRepository : IRepository<Farm>
{
    Task<Farm?> GetByUserIdAsync(Guid userId);
    Task<Farm?> GetFarmWithDetailsAsync(Guid userId);
    Task<Farm?> GetFarmWithDashboardDataAsync(Guid userId);
    Task<IReadOnlyList<Farm>> GetVerifiedFarmsByCropAsync(Guid cropTypeId, string? governorate);
    Task<List<FarmMatch>> GetFarmMatchesAsync(Guid userId, string? status, Guid? cropTypeId);
    Task<List<Contract>> GetFarmContractsAsync(Guid userId);
    Task<List<FarmMatch>> GetConversationsAsync(Guid userId);
    Task<List<Message>> GetMessagesAsync(Guid userId, Guid matchId);
    Task<List<Notification>> GetNotificationsAsync(Guid userId);
}
