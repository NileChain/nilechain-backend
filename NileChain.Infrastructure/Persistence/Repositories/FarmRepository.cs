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

    public async Task<List<FarmMatch>> GetFarmMatchesAsync(
        Guid userId,
        string? status,
        Guid? cropTypeId,
        string? sort = null)
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

        // Default: CreatedAt DESC (newest first). Score sorts available via `sort`.
        return await NileChain.Domain.Common.MatchListOrdering
            .Apply(query, sort)
            .ToListAsync();
    }

    public async Task<(List<FarmMatch> Items, int TotalCount)> GetFarmMatchesPageAsync(
        Guid userId,
        string? status,
        Guid? cropTypeId,
        string? sort,
        string? search,
        int? maxAgeDays,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = BuildFarmMatchesQuery(userId, status, cropTypeId, search, maxAgeDays);
        var total = await query.CountAsync();
        var items = await NileChain.Domain.Common.MatchListOrdering
            .Apply(query, sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<(int Total, int Proposed, int Accepted, int Rejected, int NewCount)> GetFarmMatchCountsAsync(
        Guid userId,
        DateTime newSinceUtc)
    {
        var baseQuery = Context.FarmMatches.Where(fm => fm.Farm.UserId == userId);
        var total = await baseQuery.CountAsync();
        var proposed = await baseQuery.CountAsync(fm => fm.Status == FarmMatchStatus.Proposed);
        var accepted = await baseQuery.CountAsync(fm => fm.Status == FarmMatchStatus.Accepted);
        var rejected = await baseQuery.CountAsync(fm => fm.Status == FarmMatchStatus.Rejected);
        var newCount = await baseQuery.CountAsync(fm =>
            fm.Status == FarmMatchStatus.Proposed && fm.CreatedAt >= newSinceUtc);
        return (total, proposed, accepted, rejected, newCount);
    }

    public async Task<List<FarmMatch>> GetNewFarmMatchesAsync(Guid userId, DateTime newSinceUtc, int take)
    {
        take = Math.Clamp(take, 1, 20);
        return await Context.FarmMatches
            .Include(fm => fm.Contract)
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.Factory)
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.CropType)
            .Where(fm =>
                fm.Farm.UserId == userId
                && fm.Status == FarmMatchStatus.Proposed
                && fm.CreatedAt >= newSinceUtc)
            .OrderByDescending(fm => fm.CreatedAt)
            .ThenByDescending(fm => fm.MatchId)
            .Take(take)
            .ToListAsync();
    }

    private IQueryable<FarmMatch> BuildFarmMatchesQuery(
        Guid userId,
        string? status,
        Guid? cropTypeId,
        string? search,
        int? maxAgeDays)
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

        if (maxAgeDays is > 0)
        {
            var cutoff = DateTime.UtcNow.AddDays(-maxAgeDays.Value);
            query = query.Where(fm => fm.CreatedAt >= cutoff);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(fm =>
                (fm.SupplyRequest.Factory != null
                    && fm.SupplyRequest.Factory.Name.ToLower().Contains(term))
                || (fm.SupplyRequest.Factory != null
                    && fm.SupplyRequest.Factory.Location != null
                    && fm.SupplyRequest.Factory.Location.ToLower().Contains(term))
                || (fm.SupplyRequest.CropType != null
                    && fm.SupplyRequest.CropType.Name.ToLower().Contains(term)));
        }

        return query;
    }

    public async Task<FarmMatch?> GetFarmMatchByIdAsync(Guid userId, Guid matchId) =>
        await Context.FarmMatches
            .Include(fm => fm.Contract)
            .Include(fm => fm.Farm)
                .ThenInclude(f => f.User)
            .Include(fm => fm.SupplyRequest)
                .ThenInclude(sr => sr.Factory)
                    .ThenInclude(f => f!.User)
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
            .ThenByDescending(c => c.ContractId)
            .ToListAsync();

    public async Task<Contract?> GetContractForFarmAsync(Guid userId, Guid contractId) =>
        await Context.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.Farm)
                    .ThenInclude(f => f.User)
            .Include(c => c.FarmMatch)
                .ThenInclude(fm => fm.SupplyRequest)
                    .ThenInclude(sr => sr.Factory)
                        .ThenInclude(f => f!.User)
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
