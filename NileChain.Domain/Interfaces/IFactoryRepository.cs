using NileChain.Domain.Entities;

namespace NileChain.Domain.Interfaces;

public interface IFactoryRepository : IRepository<Factory>
{
    Task<Factory?> GetByUserIdAsync(Guid userId);
    Task<Factory?> GetFactoryWithDetailsAsync(Guid userId);
    Task<SupplyRequest?> GetSupplyRequestByIdAsync(Guid requestId);
    Task<List<FarmMatch>> GetMatchesByRequestIdAsync(Guid factoryId, Guid requestId);
    Task<FarmMatch?> GetMatchForFactoryAsync(Guid factoryId, Guid matchId);
    Task<List<FarmMatch>> GetConversationsAsync(Guid factoryId);
    Task<List<Message>> GetMessagesAsync(Guid factoryId, Guid matchId);
    Task<List<Contract>> GetContractsAsync(Guid factoryId);
    Task<Contract?> GetContractForFactoryAsync(Guid factoryId, Guid contractId);
    Task<List<Notification>> GetNotificationsAsync(Guid userId);
}
