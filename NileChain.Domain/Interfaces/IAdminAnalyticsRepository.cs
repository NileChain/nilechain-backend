using NileChain.Domain.Enums;

namespace NileChain.Domain.Interfaces;

public interface IAdminAnalyticsRepository
{
    Task<int> CountUnverifiedUsersAsync(CancellationToken cancellationToken = default);
    Task<int> CountAllUsersAsync(CancellationToken cancellationToken = default);
    Task<int> CountUsersInRolesAsync(IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default);
    Task<int> CountFarmsAsync(CancellationToken cancellationToken = default);
    Task<int> CountFactoriesAsync(CancellationToken cancellationToken = default);
    Task<int> CountContractsByStatusAsync(ContractStatus status, CancellationToken cancellationToken = default);
    Task<int> CountOpenDisputesAsync(CancellationToken cancellationToken = default);
    Task<int> CountStuckFulfillmentsAsync(DateTime asOfUtcNoon, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(int Year, int Month, int Count)>> GetMonthlyContractCountsAsync(
        DateTime fromUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string CropName, decimal DemandTons, decimal? AvgPrice, decimal? AvgRisk)>> GetTopCropDemandAsync(
        int take,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(string Kind, string Message, DateTime OccurredAt, string Icon)>> GetRecentActivityAsync(
        int take,
        CancellationToken cancellationToken = default);
    Task<(int Total, IReadOnlyList<AdminContractRow> Items)> GetContractsAsync(
        string? status,
        string? search,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task<int> CountPendingWithdrawalsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(0);

    Task<IReadOnlyDictionary<Guid, LatestKybReportRow>> GetLatestKybReportsAsync(
        IReadOnlyCollection<Guid> userIds,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, LatestKybReportRow>>(
            new Dictionary<Guid, LatestKybReportRow>());

    Task<LatestKybReportRow?> GetLatestKybReportAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<LatestKybReportRow?>(null);
}

public sealed record LatestKybReportRow(
    Guid UserId,
    int TrustScore,
    string Recommendation,
    string OverallSummary,
    string BreakdownJson,
    DateTime CreatedAt);

public sealed record AdminContractRow(
    Guid ContractId,
    string FarmName,
    string FactoryName,
    string CropName,
    decimal QuantityTons,
    decimal? PricePerTon,
    decimal? FarmRiskScore,
    ContractStatus Status,
    DateTime CreatedAt);
