using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
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
            .Include(f => f.FactoryDocuments)
            .FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<SupplyRequest?> GetSupplyRequestByIdAsync(Guid requestId) =>
        await Context.SupplyRequests.FirstOrDefaultAsync(sr => sr.RequestId == requestId);

    public async Task<SupplyRequest?> GetSupplyRequestByIdempotencyKeyAsync(
        Guid factoryId,
        string idempotencyKey) =>
        await Context.SupplyRequests
            .Include(sr => sr.CropType)
            .FirstOrDefaultAsync(sr =>
                sr.FactoryId == factoryId
                && sr.IdempotencyKey == idempotencyKey);

    public async Task<(List<SupplyRequest> Items, int TotalCount)> GetSupplyRequestsPagedAsync(
        Guid factoryId,
        int page,
        int pageSize,
        string? status = null)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = Context.SupplyRequests
            .AsNoTracking()
            .Include(sr => sr.CropType)
            .Include(sr => sr.FarmMatches)
            .Where(sr => sr.FactoryId == factoryId);

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<SupplyRequestStatus>(status, ignoreCase: true, out var parsed))
        {
            query = query.Where(sr => sr.Status == parsed);
        }

        query = query.OrderByDescending(sr => sr.CreatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, total);
    }

    public async Task<SupplyRequest?> GetSupplyRequestDetailAsync(Guid factoryId, Guid requestId) =>
        await Context.SupplyRequests
            .Include(sr => sr.CropType)
            .Include(sr => sr.FarmMatches)
                .ThenInclude(m => m.Contract)
            .FirstOrDefaultAsync(sr => sr.RequestId == requestId && sr.FactoryId == factoryId);

    public async Task<Factory?> GetFactoryWithDashboardDataAsync(Guid userId) =>
        await Context.Factory
            .Include(f => f.User)
            .Include(f => f.SupplyRequests)
                .ThenInclude(sr => sr.CropType)
            .Include(f => f.SupplyRequests)
                .ThenInclude(sr => sr.FarmMatches)
                    .ThenInclude(m => m.Contract)
                        .ThenInclude(c => c!.Transactions)
            .Include(f => f.SupplyRequests)
                .ThenInclude(sr => sr.FarmMatches)
                    .ThenInclude(m => m.Contract)
                        .ThenInclude(c => c!.Fulfillment)
            .Include(f => f.SupplyRequests)
                .ThenInclude(sr => sr.FarmMatches)
                    .ThenInclude(m => m.Contract)
                        .ThenInclude(c => c!.Disputes)
            .Include(f => f.SupplyRequests)
                .ThenInclude(sr => sr.FarmMatches)
                    .ThenInclude(m => m.Farm)
            .FirstOrDefaultAsync(f => f.UserId == userId);

    public async Task<List<FarmMatch>> GetMatchesWithFarmForFactoryAsync(Guid factoryId, Guid farmId) =>
        await Context.FarmMatches
            .AsNoTracking()
            .Include(m => m.Farm)
            .Include(m => m.SupplyRequest)
                .ThenInclude(sr => sr.CropType)
            .Include(m => m.Contract)
                .ThenInclude(c => c!.Fulfillment)
            .Include(m => m.Contract)
                .ThenInclude(c => c!.Disputes)
            .Where(m =>
                m.FarmId == farmId
                && m.SupplyRequest.FactoryId == factoryId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();

    public async Task<List<FarmMatch>> GetMatchesByRequestIdAsync(
        Guid factoryId,
        Guid requestId,
        string? sort = null) =>
        await NileChain.Domain.Common.MatchListOrdering
            .Apply(
                Context.FarmMatches
                    .Include(fm => fm.Farm)
                    .Include(fm => fm.Contract)
                    .Include(fm => fm.NegotiationRounds)
                    .Include(fm => fm.SupplyRequest)
                        .ThenInclude(sr => sr.CropType)
                    .Where(fm =>
                        fm.RequestId == requestId &&
                        fm.SupplyRequest.FactoryId == factoryId &&
                        fm.Status != FarmMatchStatus.Rejected &&
                        fm.Status != FarmMatchStatus.Expired),
                sort)
            .ToListAsync();

    public async Task<FarmMatch?> GetMatchForFactoryAsync(Guid factoryId, Guid matchId) =>
        await Context.FarmMatches
            .Include(m => m.Farm)
                .ThenInclude(f => f.User)
            .Include(m => m.SupplyRequest)
                .ThenInclude(r => r.Factory)
                    .ThenInclude(f => f!.User)
            .Include(m => m.SupplyRequest)
                .ThenInclude(r => r.CropType)
            .Include(m => m.Contract)
            .Include(m => m.NegotiationRounds)
            .FirstOrDefaultAsync(m => m.MatchId == matchId && m.SupplyRequest.FactoryId == factoryId);

    public async Task<List<FarmCrop>> GetPublishedFarmCropsAsync(Guid? cropTypeId, string? governorate)
    {
        var query = Context.FarmCrops
            .AsNoTracking()
            .Include(fc => fc.CropType)
            .Include(fc => fc.Farm)
                .ThenInclude(f => f.User)
            .Include(fc => fc.Farm)
                .ThenInclude(f => f.FarmImages)
            .Where(fc => fc.IsPublished)
            .Where(fc => fc.Farm.User.IsActive);

        if (cropTypeId is Guid cropId)
            query = query.Where(fc => fc.CropTypeId == cropId);

        if (!string.IsNullOrWhiteSpace(governorate))
        {
            var gov = governorate.Trim().ToLowerInvariant();
            query = query.Where(fc =>
                fc.Farm.Governorate != null
                && fc.Farm.Governorate.ToLower() == gov);
        }

        return await query
            .OrderByDescending(fc => fc.Farm.IsVerified)
            .ThenByDescending(fc => fc.Farm.RiskScore ?? 0)
            .ThenBy(fc => fc.Farm.Name)
            .Take(100)
            .ToListAsync();
    }

    public async Task<List<FarmMatch>> GetConversationsAsync(Guid factoryId) =>
        await Context.FarmMatches
            .Include(m => m.Farm)
            .Include(m => m.SupplyRequest)
                .ThenInclude(r => r!.CropType)
            .Include(m => m.Messages)
            .Include(m => m.Contract)
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
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r!.Factory)
            .Where(c => c.FarmMatch.SupplyRequest.FactoryId == factoryId)
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.ContractId)
            .ToListAsync();

    public async Task<Contract?> GetContractForFactoryAsync(Guid factoryId, Guid contractId) =>
        await Context.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.Farm)
                    .ThenInclude(f => f.User)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.Farm)
                    .ThenInclude(f => f.FarmCrops)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r!.CropType)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r!.Factory)
                        .ThenInclude(f => f!.User)
            .Include(c => c.IntegrityAnchors)
            .Include(c => c.Revisions)
            .Include(c => c.Fulfillment)
            .FirstOrDefaultAsync(c =>
                c.ContractId == contractId && c.FarmMatch.SupplyRequest.FactoryId == factoryId);

    public async Task<List<Notification>> GetNotificationsAsync(Guid userId) =>
        await Context.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();
}
