namespace NileChain.Application.Dtos.Farm;

public class FarmContractDto
{
    public Guid ContractId { get; set; }
    public Guid MatchId { get; set; }
    public string FactoryName { get; set; } = default!;
    public string? FactoryLocation { get; set; }
    public string FarmName { get; set; } = default!;
    public string CropName { get; set; } = default!;
    public decimal QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public string? DeliveryLocation { get; set; }
    public string? GeneratedText { get; set; }
    public string? PdfUrl { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? SignedAt { get; set; }
    public bool FactorySigned { get; set; }
    public bool FarmSigned { get; set; }
    public DateTime? FactorySignedAt { get; set; }
    public DateTime? FarmSignedAt { get; set; }
    public Guid? FarmUserId { get; set; }
    public Guid? FactoryUserId { get; set; }
    public bool CanUnwindSigned { get; set; }
    public DateTime UpdatedAt { get; set; }
    public decimal? MatchScore { get; set; }
    public decimal? RiskScore { get; set; }
    public NileChain.Application.Dtos.Integrity.ContractIntegrityDto? Integrity { get; set; }
}
