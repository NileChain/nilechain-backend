namespace NileChain.Application.Dtos.Integrity;

public class ContractIntegrityDto
{
    public Guid AnchorId { get; set; }
    public Guid ContractId { get; set; }
    public string ContentHash { get; set; } = default!;
    public string ShortHash { get; set; } = default!;
    public string PreviousHash { get; set; } = default!;
    public long ChainIndex { get; set; }
    public string TxRef { get; set; } = default!;
    public string Status { get; set; } = default!;
    public DateTime AnchoredAtUtc { get; set; }
}

public class ContractIntegrityVerifyDto
{
    public string Outcome { get; set; } = default!;
    public string ContentHash { get; set; } = default!;
    public string? PreviousHash { get; set; }
    public long? ChainIndex { get; set; }
    public string? TxRef { get; set; }
    public DateTime? AnchoredAtUtc { get; set; }
    public string? Status { get; set; }
    public Guid? ContractId { get; set; }
    public string? FarmName { get; set; }
    public string? FactoryName { get; set; }
    public string? CropName { get; set; }
    public DateTime? SignedAt { get; set; }
    public bool CurrentContentMatches { get; set; }
    public string HonestyNote { get; set; } =
        "Integrity chain for signed contract content. Not on-chain payment settlement.";
}
