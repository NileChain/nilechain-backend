namespace NileChain.Application.Dtos.Factory;

public class FactoryContractDto
{
    public Guid ContractId { get; set; }
    public Guid MatchId { get; set; }
    public string FarmName { get; set; } = default!;
    public string? FarmLocation { get; set; }
    public string FactoryName { get; set; } = default!;
    public string? CropName { get; set; }
    public decimal QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public string? QualityRequirements { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool HasPendingDateAmendment { get; set; }
    public DateTime? PendingStartsAt { get; set; }
    public DateTime? PendingEndsAt { get; set; }
    public Guid? DateAmendmentProposedByUserId { get; set; }
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
    public NileChain.Application.Dtos.Contracts.ContractRevisionDto? LastRevision { get; set; }
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
