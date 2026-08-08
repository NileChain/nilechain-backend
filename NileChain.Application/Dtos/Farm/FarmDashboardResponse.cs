namespace NileChain.Application.Dtos.Farm;

public class FarmDashboardResponse
{
    public decimal? RiskScore { get; set; }
    public int ActiveMatchesCount { get; set; }
    public int CompletedContractsCount { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public List<RiskBreakdownItem> RiskBreakdown { get; set; } = new();
    public List<RecentMatchItem> RecentMatches { get; set; } = new();
    public List<ImprovementTip> ImprovementTips { get; set; } = new();
    /// <summary>Chronological reliability/risk scores (0–100) for sparkline.</summary>
    public List<ReliabilityTrendPoint> ReliabilityTrend { get; set; } = new();
}

public class ReliabilityTrendPoint
{
    public decimal Value { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class RiskBreakdownItem
{
    public string Label { get; set; } = default!;
    public decimal Percentage { get; set; }
}

public class RecentMatchItem
{
    public Guid MatchId { get; set; }
    public string FactoryName { get; set; } = default!;
    public string CropName { get; set; } = default!;
    public decimal QuantityTons { get; set; }
    public decimal? MatchScore { get; set; }
    public string Status { get; set; } = default!;
}

public class ImprovementTip
{
    public string Category { get; set; } = default!;
    public decimal CurrentScore { get; set; }
    public string Severity { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string Icon { get; set; } = default!;
}
