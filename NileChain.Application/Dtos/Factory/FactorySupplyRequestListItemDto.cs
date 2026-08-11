namespace NileChain.Application.Dtos.Factory;

public class FactorySupplyRequestListItemDto
{
    public Guid RequestId { get; set; }
    public string Crop { get; set; } = string.Empty;
    public decimal QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? IdempotencyKey { get; set; }
}
