namespace NileChain.Application.Dtos.Farm;

public class FarmMatchItemDto
{
    public Guid MatchId { get; set; }
    public Guid? FactoryId { get; set; }
    public string FactoryName { get; set; } = default!;
    public string? FactoryLocation { get; set; }
    public bool FactoryIsVerified { get; set; }
    public string CropName { get; set; } = default!;
    public Guid CropTypeId { get; set; }
    public decimal QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? QualitySpecs { get; set; }
    public decimal? MatchScore { get; set; }
    public decimal? RiskScore { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public Guid? ContractId { get; set; }
    public bool ContractFullySigned { get; set; }
    public bool CanMessage { get; set; }

    public decimal? CounterQuantityTons { get; set; }
    public decimal? CounterPricePerTon { get; set; }
    public DateTime? CounterDeliveryDate { get; set; }
    public string? CounterNote { get; set; }
    public DateTime? CounteredAt { get; set; }
    public bool CounterAccepted { get; set; }
    public decimal EffectiveQuantityTons { get; set; }
    public decimal? EffectivePricePerTon { get; set; }
    public DateTime? EffectiveDeliveryDate { get; set; }
}
