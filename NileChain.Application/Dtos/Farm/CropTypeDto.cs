namespace NileChain.Application.Dtos.Farm;

public class CropTypeDto
{
    public Guid CropTypeId { get; set; }
    public string Name { get; set; } = default!;
    public decimal? AvailableQuantityTons { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }
    public decimal? MinPricePerTon { get; set; }
    public bool IsPublished { get; set; }
}
