namespace NileChain.Application.Dtos.Farm;

public class AddCropRequest
{
    public Guid CropTypeId { get; set; }
    public decimal? AvailableQuantityTons { get; set; }
    public DateTime? AvailableFrom { get; set; }
    public DateTime? AvailableTo { get; set; }
    public decimal? MinPricePerTon { get; set; }
    public bool IsPublished { get; set; } = true;
}
