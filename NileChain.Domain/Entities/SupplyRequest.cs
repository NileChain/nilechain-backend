using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

public class SupplyRequest
{
    public Guid RequestId { get; set; }
    public Guid FactoryId { get; set; }
    public Guid CropTypeId { get; set; }
    public decimal QuantityTons { get; set; }
    public string? QualitySpecs { get; set; }
    public decimal? PricePerTon { get; set; }
    /// <summary>
    /// Egypt calendar delivery date, stored at 12:00 UTC (see <see cref="Common.DeliveryDatePolicy"/>).
    /// </summary>
    public DateTime? DeliveryDate { get; set; }

    /// <summary>Where goods change hands. Default factory gate.</summary>
    public DeliveryPoint DeliveryPoint { get; set; } = DeliveryPoint.FactoryGate;

    public DealParty FreightPayer { get; set; } = DealParty.Farm;
    public DealParty TransitRisk { get; set; } = DealParty.Farm;

    public SupplyRequestStatus Status { get; set; } = SupplyRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional client idempotency key; unique per factory when set (filtered unique index).
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public Factory Factory { get; set; } = default!;
    public CropType CropType { get; set; } = default!;
    public ICollection<FarmMatch> FarmMatches { get; set; } = new List<FarmMatch>();
    public ICollection<ComparisonReport> ComparisonReports { get; set; } = new List<ComparisonReport>();
}
