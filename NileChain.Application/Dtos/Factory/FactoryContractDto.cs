namespace NileChain.Application.Dtos.Factory;

public class FactoryContractDto
{
    public Guid ContractId { get; set; }
    public Guid MatchId { get; set; }
    public string FarmName { get; set; } = default!;
    public string? CropName { get; set; }
    public decimal QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public string? GeneratedText { get; set; }
    public string? PdfUrl { get; set; }
    public string Status { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime? SignedAt { get; set; }
}

public class PersistContractRequest
{
    public Guid MatchId { get; set; }
    public string ContractText { get; set; } = string.Empty;
}

public class PersistContractResponse
{
    public Guid ContractId { get; set; }
    public string Status { get; set; } = string.Empty;
}
