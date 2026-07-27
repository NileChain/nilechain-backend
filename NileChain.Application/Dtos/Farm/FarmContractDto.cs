namespace NileChain.Application.Dtos.Farm;

public class FarmContractDto
{
    public Guid ContractId { get; set; }
    public Guid MatchId { get; set; }
    public string FactoryName { get; set; } = default!;
    public string? FactoryLocation { get; set; }
    public string CropName { get; set; } = default!;
    public decimal QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? SignedAt { get; set; }
}
