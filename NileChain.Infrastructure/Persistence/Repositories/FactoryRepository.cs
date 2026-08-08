using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class FactoryRepository : Repository<Factory>, IFactoryRepository
{
    public FactoryRepository(NileChainDbContext context) : base(context) { }

    public async Task<Factory?> GetByUserIdAsync(Guid userId) =>
        await Context.Factory.FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<Factory?> GetFactoryWithDetailsAsync(Guid userId) =>
        await Context.Factory
            .Include(f => f.User)
            .FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<SupplyRequest?> GetSupplyRequestByIdAsync(Guid requestId) =>
        await Context.SupplyRequests.FirstOrDefaultAsync(sr => sr.RequestId == requestId);

    public async Task<List<FarmMatch>> GetMatchesByRequestIdAsync(Guid factoryId, Guid requestId) =>
        await Context.FarmMatches
            .Include(fm => fm.Farm)
            .Where(fm => fm.RequestId == requestId && fm.SupplyRequest.FactoryId == factoryId)
            .OrderByDescending(fm => fm.MatchScore)
            .ToListAsync();

    public async Task<FarmMatch?> GetMatchForFactoryAsync(Guid factoryId, Guid matchId) =>
        await Context.FarmMatches
            .Include(m => m.Farm)
            .Include(m => m.SupplyRequest)
                .ThenInclude(r => r.Factory)
            .Include(m => m.SupplyRequest)
                .ThenInclude(r => r.CropType)
            .Include(m => m.Contract)
            .FirstOrDefaultAsync(m => m.MatchId == matchId && m.SupplyRequest.FactoryId == factoryId);

    public async Task<List<FarmMatch>> GetConversationsAsync(Guid factoryId) =>
        await Context.FarmMatches
            .Include(m => m.Farm)
            .Include(m => m.SupplyRequest)
                .ThenInclude(r => r!.CropType)
            .Include(m => m.Messages)
            .Where(m => m.SupplyRequest.FactoryId == factoryId)
            .ToListAsync();

    public async Task<List<Message>> GetMessagesAsync(Guid factoryId, Guid matchId) =>
        await Context.Messages
            .Include(m => m.Sender)
            .Include(m => m.FarmMatch)
                .ThenInclude(fm => fm.SupplyRequest)
            .Where(m => m.MatchId == matchId && m.FarmMatch.SupplyRequest.FactoryId == factoryId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    public async Task<List<Contract>> GetContractsAsync(Guid factoryId) =>
        await Context.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.Farm)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r!.CropType)
            .Where(c => c.FarmMatch.SupplyRequest.FactoryId == factoryId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<Contract?> GetContractForFactoryAsync(Guid factoryId, Guid contractId) =>
        await Context.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.Farm)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r!.CropType)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r!.Factory)
            .FirstOrDefaultAsync(c =>
                c.ContractId == contractId && c.FarmMatch.SupplyRequest.FactoryId == factoryId);

    public async Task<List<Notification>> GetNotificationsAsync(Guid userId) =>
        await Context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
}
