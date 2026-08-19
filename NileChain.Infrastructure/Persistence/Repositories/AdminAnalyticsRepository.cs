using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;

namespace NileChain.Infrastructure.Persistence.Repositories;

public sealed class AdminAnalyticsRepository : IAdminAnalyticsRepository
{
    private readonly NileChainDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AdminAnalyticsRepository(NileChainDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public Task<int> CountUnverifiedUsersAsync(CancellationToken cancellationToken = default) =>
        _userManager.Users.CountAsync(
            u => !u.IsVerified
                && u.IsActive
                && u.KybReviewStatus != NileChain.Domain.Enums.KybReviewStatus.Rejected,
            cancellationToken);

    public Task<int> CountAllUsersAsync(CancellationToken cancellationToken = default) =>
        _userManager.Users.CountAsync(cancellationToken);

    public async Task<int> CountUsersInRolesAsync(
        IReadOnlyCollection<string> roleNames,
        CancellationToken cancellationToken = default)
    {
        if (roleNames.Count == 0)
            return 0;

        var roleIds = await _db.Roles
            .AsNoTracking()
            .Where(r => roleNames.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (roleIds.Count == 0)
            return 0;

        return await _db.UserRoles
            .AsNoTracking()
            .Where(ur => roleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .CountAsync(cancellationToken);
    }

    public Task<int> CountFarmsAsync(CancellationToken cancellationToken = default) =>
        _db.Farm.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountFactoriesAsync(CancellationToken cancellationToken = default) =>
        _db.Factory.AsNoTracking().CountAsync(cancellationToken);

    public Task<int> CountContractsByStatusAsync(
        ContractStatus status,
        CancellationToken cancellationToken = default) =>
        _db.Contracts.AsNoTracking().CountAsync(c => c.Status == status, cancellationToken);

    public Task<int> CountOpenDisputesAsync(CancellationToken cancellationToken = default) =>
        _db.Disputes.AsNoTracking().CountAsync(
            d => d.Status == DisputeStatus.Open || d.Status == DisputeStatus.UnderReview,
            cancellationToken);

    public Task<int> CountStuckFulfillmentsAsync(
        DateTime asOfUtcNoon,
        CancellationToken cancellationToken = default) =>
        _db.Fulfillments.AsNoTracking().CountAsync(
            f => f.Status == FulfillmentStatus.Planned
                 && f.PlannedShipDate != null
                 && f.PlannedShipDate < asOfUtcNoon,
            cancellationToken);

    public async Task<IReadOnlyList<(int Year, int Month, int Count)>> GetMonthlyContractCountsAsync(
        DateTime fromUtc,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Contracts
            .AsNoTracking()
            .Where(c => c.CreatedAt >= fromUtc)
            .GroupBy(c => new { c.CreatedAt.Year, c.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => (r.Year, r.Month, r.Count))
            .OrderBy(r => r.Year)
            .ThenBy(r => r.Month)
            .ToList();
    }

    public async Task<IReadOnlyList<(string CropName, decimal DemandTons, decimal? AvgPrice, decimal? AvgRisk)>> GetTopCropDemandAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.SupplyRequests
            .AsNoTracking()
            .GroupBy(r => r.CropType.Name)
            .Select(g => new
            {
                CropName = g.Key,
                DemandTons = g.Sum(r => r.QuantityTons),
                AvgPrice = g.Average(r => r.PricePerTon),
                AvgRisk = g
                    .SelectMany(r => r.FarmMatches)
                    .Where(m => m.RiskScore != null)
                    .Average(m => (decimal?)m.RiskScore)
            })
            .OrderByDescending(x => x.DemandTons)
            .Take(take)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => (r.CropName, r.DemandTons, r.AvgPrice, r.AvgRisk))
            .ToList();
    }

    public async Task<IReadOnlyList<(string Kind, string Message, DateTime OccurredAt, string Icon)>> GetRecentActivityAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        var contractRows = await _db.Contracts
            .AsNoTracking()
            .OrderByDescending(c => c.CreatedAt)
            .Take(take)
            .Select(c => new { c.ContractId, c.Status, c.CreatedAt })
            .ToListAsync(cancellationToken);

        var contractEvents = contractRows.Select(c => (
            Kind: "contract",
            Message: $"Contract {c.ContractId.ToString()[..8].ToUpperInvariant()} — {c.Status}",
            OccurredAt: c.CreatedAt,
            Icon: "description"
        ));

        var disputeRows = await _db.Disputes
            .AsNoTracking()
            .OrderByDescending(d => d.CreatedAt)
            .Take(take)
            .Select(d => new { d.Type, d.Status, d.CreatedAt })
            .ToListAsync(cancellationToken);

        var disputeEvents = disputeRows.Select(d => (
            Kind: "dispute",
            Message: $"Dispute {d.Type} — {d.Status}",
            OccurredAt: d.CreatedAt,
            Icon: "gavel"
        ));

        var fulfillmentRows = await _db.FulfillmentEvents
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(take)
            .Select(e => new { e.ToStatus, e.CreatedAt })
            .ToListAsync(cancellationToken);

        var fulfillmentEvents = fulfillmentRows.Select(e => (
            Kind: "fulfillment",
            Message: $"Fulfillment → {e.ToStatus}",
            OccurredAt: e.CreatedAt,
            Icon: "local_shipping"
        ));

        return contractEvents
            .Concat(disputeEvents)
            .Concat(fulfillmentEvents)
            .OrderByDescending(x => x.OccurredAt)
            .Take(take)
            .Select(x => (x.Kind, x.Message, x.OccurredAt, x.Icon))
            .ToList();
    }

    public async Task<(int Total, IReadOnlyList<AdminContractRow> Items)> GetContractsAsync(
        string? status,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Contracts
            .AsNoTracking()
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.Farm)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r.Factory)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r.CropType)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)
            && Enum.TryParse<ContractStatus>(status.Trim(), ignoreCase: true, out var parsed))
        {
            query = query.Where(c => c.Status == parsed);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(c =>
                c.FarmMatch.Farm.Name.ToLower().Contains(term)
                || c.FarmMatch.SupplyRequest.Factory.Name.ToLower().Contains(term)
                || c.FarmMatch.SupplyRequest.CropType.Name.ToLower().Contains(term)
                || c.ContractId.ToString().ToLower().Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var page = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        var items = page.Select(c =>
        {
            var supply = c.FarmMatch.SupplyRequest;
            return new AdminContractRow(
                c.ContractId,
                c.FarmMatch.Farm?.Name ?? "—",
                supply?.Factory?.Name ?? "—",
                supply?.CropType?.Name ?? "—",
                supply?.QuantityTons ?? 0m,
                supply?.PricePerTon,
                c.FarmMatch.Farm?.RiskScore ?? c.FarmMatch.RiskScore,
                c.Status,
                c.CreatedAt);
        }).ToList();

        return (total, items);
    }

    public Task<int> CountPendingWithdrawalsAsync(CancellationToken cancellationToken = default) =>
        _db.WalletWithdrawals.AsNoTracking().CountAsync(
            w => w.Status == WalletWithdrawalStatus.Pending
                 || w.Status == WalletWithdrawalStatus.Processing,
            cancellationToken);

    public async Task<IReadOnlyDictionary<Guid, LatestKybReportRow>> GetLatestKybReportsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
            return new Dictionary<Guid, LatestKybReportRow>();

        var idSet = userIds.ToHashSet();
        var rows = await _db.KybVerificationReports
            .AsNoTracking()
            .Where(r => idSet.Contains(r.UserId))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.UserId)
            .Select(g => g.First())
            .ToDictionary(
                r => r.UserId,
                r => new LatestKybReportRow(
                    r.UserId,
                    r.TrustScore,
                    r.Recommendation,
                    r.OverallSummary,
                    r.BreakdownJson,
                    r.CreatedAt));
    }

    public async Task<LatestKybReportRow?> GetLatestKybReportAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var row = await _db.KybVerificationReports
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new LatestKybReportRow(
                row.UserId,
                row.TrustScore,
                row.Recommendation,
                row.OverallSummary,
                row.BreakdownJson,
                row.CreatedAt);
    }
}
