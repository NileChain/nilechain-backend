namespace NileChain.Application.Dtos.Factory;

public class FarmListingDto
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = default!;
    public string? Governorate { get; set; }
    public bool IsVerified { get; set; }
    public decimal? RiskScore { get; set; }
    public decimal AverageRating { get; set; }
    public Guid CropTypeId { get; set; }
    public string CropName { get; set; } = default!;
    public decimal? AvailableQuantityTons { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }
    public decimal? MinPricePerTon { get; set; }
    public string? CoverImageUrl { get; set; }
}
