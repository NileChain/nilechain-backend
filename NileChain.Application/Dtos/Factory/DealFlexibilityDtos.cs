namespace NileChain.Application.Dtos.Factory;

public class UpdateSupplyRequestGeoScopeRequest
{
    public string GeographicScope { get; set; } = string.Empty;
    public List<string>? SelectedGovernorates { get; set; }
}

public class MatchNegotiationRoundDto
{
    public Guid RoundId { get; set; }
    public string OfferedBy { get; set; } = string.Empty;
    public decimal? QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? Grade { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
