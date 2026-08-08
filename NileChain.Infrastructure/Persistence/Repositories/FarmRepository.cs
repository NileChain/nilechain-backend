using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Infrastructure.Persistence.Repositories;

public class FarmRepository : Repository<Farm>, IFarmRepository
{
    public FarmRepository(NileChainDbContext context) : base(context) { }

    public async Task<Farm?> GetByUserIdAsync(Guid userId) =>
        await Context.Farm.FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<Farm?> GetFarmWithDetailsAsync(Guid userId) =>
        await Context.Farm
            .Include(f => f.User)
            .Include(f => f.CropTypes)
            .Include(f => f.FarmDocuments)
            .FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<Farm?> GetFarmWithDashboardDataAsync(Guid userId) =>
        await Context.Farm
            .Include(f => f.User)
            .Include(f => f.CropTypes)
            .Include(f => f.FarmDocuments)
            .Include(f => f.FarmCertifications)
            .Include(f => f.FarmMatches)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.Factory)
            .Include(f => f.FarmMatches)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.CropType)
            .Include(f => f.FarmMatches)
                .ThenInclude(fm => fm.Contract)
            .FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<List<FarmMatch>> GetFarmMatchesAsync(Guid userId, string? status, Guid? cropTypeId)
    {
        var query = Context.FarmMatches
            .Include(fm => fm.Contract)
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.Factory)
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.CropType)
            .Where(fm => fm.Farm.UserId == userId);

        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<FarmMatchStatus>(status, out var parsedStatus))
            query = query.Where(fm => fm.Status == parsedStatus);

        if (cropTypeId.HasValue)
            query = query.Where(fm => fm.SupplyRequest.CropTypeId == cropTypeId.Value);

        return await query
            .OrderByDescending(fm => fm.MatchScore)
            .ToListAsync();
    }

    public async Task<FarmMatch?> GetFarmMatchByIdAsync(Guid userId, Guid matchId) =>
        await Context.FarmMatches
            .Include(fm => fm.Contract)
            .Include(fm => fm.Farm)
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.Factory)
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.CropType)
            .FirstOrDefaultAsync(fm => fm.MatchId == matchId && fm.Farm.UserId == userId);

    public async Task<IReadOnlyList<Farm>> GetVerifiedFarmsByCropAsync(Guid cropTypeId, string? governorate) =>
        await Context.Farm
            .Where(f => f.IsVerified && f.CropTypes.Any(c => c.CropTypeId == cropTypeId))
            .Where(f => governorate == null || f.Governorate == governorate)
            .ToListAsync();

    public async Task<List<Contract>> GetFarmContractsAsync(Guid userId) =>
        await Context.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.Farm)
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.Factory)
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.CropType)
            .Where(c => c.FarmMatch.Farm.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();

    public async Task<Contract?> GetContractForFarmAsync(Guid userId, Guid contractId) =>
        await Context.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.Farm)
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.Factory)
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.CropType)
            .FirstOrDefaultAsync(c =>
                c.ContractId == contractId && c.FarmMatch.Farm.UserId == userId);

    public async Task<Contract?> GetContractByMatchForFarmAsync(Guid userId, Guid matchId) =>
        await Context.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.Farm)
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.Factory)
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.CropType)
            .FirstOrDefaultAsync(c =>
                c.MatchId == matchId && c.FarmMatch.Farm.UserId == userId);

    public async Task<List<FarmMatch>> GetConversationsAsync(Guid userId) =>
        await Context.FarmMatches
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.Factory)
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.CropType)
            .Include(fm => fm.Messages)
                .ThenInclude(m => m.Sender)
            .Where(fm => fm.Farm.UserId == userId && fm.Messages.Count > 0)
            .OrderByDescending(fm => fm.Messages.Max(m => m.CreatedAt))
            .ToListAsync();

    public async Task<List<Message>> GetMessagesAsync(Guid userId, Guid matchId) =>
        await Context.Messages
            .Include(m => m.Sender)
            .Include(m => m.FarmMatch)
            .Where(m => m.MatchId == matchId && m.FarmMatch.Farm.UserId == userId)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync();

    public async Task<List<Notification>> GetNotificationsAsync(Guid userId) =>
        await Context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
}
