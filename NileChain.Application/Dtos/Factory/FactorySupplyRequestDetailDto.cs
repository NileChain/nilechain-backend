namespace NileChain.Application.Dtos.Factory;

public class FactorySupplyRequestDetailDto
{
    public Guid RequestId { get; set; }
    public Guid CropTypeId { get; set; }
    public string Crop { get; set; } = string.Empty;
    public decimal QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? QualitySpecsRaw { get; set; }
    public StructuredQualitySpecsDto Quality { get; set; } = new();
    public int MatchCount { get; set; }
    public int ActiveMatchCount { get; set; }
    public bool CanCancel { get; set; }
    public bool CanRerunAgent { get; set; }
    public bool CanUpdateDeliveryTerms { get; set; }
    public string DeliveryPoint { get; set; } = "FactoryGate";
    public string FreightPayer { get; set; } = "Farm";
    public string TransitRisk { get; set; } = "Farm";
}
