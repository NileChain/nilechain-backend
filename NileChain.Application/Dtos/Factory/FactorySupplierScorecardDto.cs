namespace NileChain.Application.Dtos.Factory;

public class FactorySupplierScorecardDto
{
    public Guid FarmId { get; set; }
    public Guid? FarmUserId { get; set; }
    public string FarmName { get; set; } = default!;
    public string? Governorate { get; set; }
    public bool IsVerified { get; set; }
    public decimal? RiskScore { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int DealsWithThisFactory { get; set; }
    public int CompletedContracts { get; set; }
    public int OpenDisputes { get; set; }
    public int TotalDisputes { get; set; }
    public decimal? OnTimeFulfillmentRate { get; set; }
    public decimal? QcIssueRate { get; set; }
    public decimal? AverageQcDiscountPercent { get; set; }
    public List<FactorySupplierDealDto> RecentDeals { get; set; } = new();
}

public class FactorySupplierDealDto
{
    public Guid ContractId { get; set; }
    public Guid? MatchId { get; set; }
    public string Crop { get; set; } = default!;
    public decimal QuantityTons { get; set; }
    public string ContractStatus { get; set; } = default!;
    public string? FulfillmentStatus { get; set; }
    public DateTime? SignedAt { get; set; }
    public decimal? QcDiscountPercent { get; set; }
}
