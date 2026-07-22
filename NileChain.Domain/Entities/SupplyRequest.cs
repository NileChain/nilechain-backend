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
    public DateTime? DeliveryDate { get; set; }
    public SupplyRequestStatus Status { get; set; } = SupplyRequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Factory Factory { get; set; } = default!;
    public CropType CropType { get; set; } = default!;
    public ICollection<FarmMatch> FarmMatches { get; set; } = new List<FarmMatch>();
    public ICollection<ComparisonReport> ComparisonReports { get; set; } = new List<ComparisonReport>();
}
