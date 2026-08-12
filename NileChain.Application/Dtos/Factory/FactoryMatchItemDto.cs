namespace NileChain.Application.Dtos.Factory;

public class FactoryMatchItemDto
{
    public Guid MatchId { get; set; }
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = default!;
    public string? FarmLocation { get; set; }
    public string? FarmGovernorate { get; set; }
    public decimal? FarmLatitude { get; set; }
    public decimal? FarmLongitude { get; set; }
    public bool FarmIsVerified { get; set; }
    public decimal FarmAverageRating { get; set; }
    public decimal? MatchScore { get; set; }
    public decimal? RiskScore { get; set; }
    /// <summary>Factory↔farm haversine km when both have coordinates; otherwise null.</summary>
    public double? DistanceKm { get; set; }
    /// <summary>True when distance could not be computed and governorate matching was used instead.</summary>
    public bool UsedGovernorateFallback { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public Guid? ContractId { get; set; }
    public bool ContractFullySigned { get; set; }
    public bool CanMessage { get; set; }

    public decimal? RequestQuantityTons { get; set; }
    public decimal? RequestPricePerTon { get; set; }
    public DateTime? RequestDeliveryDate { get; set; }
    public decimal? CounterQuantityTons { get; set; }
    public decimal? CounterPricePerTon { get; set; }
    public DateTime? CounterDeliveryDate { get; set; }
    public string? CounterNote { get; set; }
    public bool CounterAccepted { get; set; }
    public decimal EffectiveQuantityTons { get; set; }
    public decimal? EffectivePricePerTon { get; set; }
    public DateTime? EffectiveDeliveryDate { get; set; }
}
