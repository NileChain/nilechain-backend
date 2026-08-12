namespace NileChain.Application.Dtos.Factory;

public class FactoryDashboardResponse
{
    public int OpenRequestsCount { get; set; }
    public int ActiveMatchesCount { get; set; }
    public int ActiveContractsCount { get; set; }
    public int CompletedContractsCount { get; set; }
    public decimal TotalProcurementValue { get; set; }
    public decimal AverageSupplierRiskScore { get; set; }
    public FactoryPayablesSummaryDto PayablesSummary { get; set; } = new();
    public List<FactoryAttentionItemDto> Attention { get; set; } = new();
    public List<FactorySupplyRequestListItemDto> RecentRequests { get; set; } = new();
}

public class FactoryPayablesSummaryDto
{
    public decimal PendingAmount { get; set; }
    public decimal AwaitingFarmConfirmAmount { get; set; }
    public decimal PaidConfirmedAmount { get; set; }
    public decimal OverdueAmount { get; set; }
    public string Currency { get; set; } = "EGP";
}

public class FactoryAttentionItemDto
{
    public string Id { get; set; } = default!;
    public string Kind { get; set; } = default!;
    public string Tone { get; set; } = "attention";
    public int Count { get; set; }
    public string Title { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Cta { get; set; } = default!;
    public string Link { get; set; } = default!;
    public Guid? EntityId { get; set; }
}
