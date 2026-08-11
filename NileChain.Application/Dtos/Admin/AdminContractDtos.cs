namespace NileChain.Application.Dtos.Admin;

public sealed class AdminContractListItemDto
{
    public Guid ContractId { get; init; }
    public string ShortId { get; init; } = string.Empty;
    public string FarmName { get; init; } = string.Empty;
    public string FactoryName { get; init; } = string.Empty;
    public string CropName { get; init; } = string.Empty;
    public decimal? QuantityTons { get; init; }
    public decimal? ValueEgp { get; init; }
    public decimal? FarmRiskScore { get; init; }
    public string RiskBand { get; init; } = "medium";
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}

public sealed class AdminContractListDto
{
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public IReadOnlyList<AdminContractListItemDto> Items { get; init; } =
        Array.Empty<AdminContractListItemDto>();
}
