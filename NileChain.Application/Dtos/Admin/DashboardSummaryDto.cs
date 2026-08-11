namespace NileChain.Application.Dtos.Admin;

public sealed class DashboardSummaryDto
{
    public int PendingVerifications { get; init; }
    public int OpenDisputes { get; init; }
    public int StuckFulfillments { get; init; }
    public int PendingSignatureContracts { get; init; }
    public int SignedContracts { get; init; }
    public int FarmCount { get; init; }
    public int FactoryCount { get; init; }
    public int AdminCount { get; init; }
    public int TotalUsers { get; init; }
    public IReadOnlyList<MonthlyContractPointDto> MonthlyContracts { get; init; } =
        Array.Empty<MonthlyContractPointDto>();
    public IReadOnlyList<CropDemandDto> TopCrops { get; init; } =
        Array.Empty<CropDemandDto>();
    public IReadOnlyList<AdminActivityItemDto> RecentActivity { get; init; } =
        Array.Empty<AdminActivityItemDto>();
}

public sealed class MonthlyContractPointDto
{
    public string Label { get; init; } = string.Empty;
    public int Count { get; init; }
    /// <summary>0–100 bar height for the UI chart.</summary>
    public int HeightPercent { get; init; }
}

public sealed class CropDemandDto
{
    public string CropName { get; init; } = string.Empty;
    public decimal DemandTons { get; init; }
    public decimal? AvgPricePerTon { get; init; }
    /// <summary>Average farm risk score for matches on this crop (0–100 trust), or null.</summary>
    public decimal? AvgRiskScore { get; init; }
    public string RiskBand { get; init; } = "medium";
}

public sealed class AdminActivityItemDto
{
    public string Kind { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime OccurredAt { get; init; }
    public string Icon { get; init; } = "info";
}
