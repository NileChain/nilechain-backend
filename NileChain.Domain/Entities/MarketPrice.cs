namespace NileChain.Domain.Entities;

public class MarketPrice
{
    public Guid PriceId { get; set; }
    public Guid CropTypeId { get; set; }
    public string? Governorate { get; set; }
    public decimal PricePerTon { get; set; }
    public string? Source { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    public CropType CropType { get; set; } = default!;
}
