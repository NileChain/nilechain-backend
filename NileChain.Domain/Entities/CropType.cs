namespace NileChain.Domain.Entities;

public class CropType
{
    public Guid CropTypeId { get; set; }
    public string Name { get; set; } = default!;

    public ICollection<Farm> Farms { get; set; } = new List<Farm>();
    public ICollection<SupplyRequest> SupplyRequests { get; set; } = new List<SupplyRequest>();
    public ICollection<MarketPrice> MarketPrices { get; set; } = new List<MarketPrice>();
}
