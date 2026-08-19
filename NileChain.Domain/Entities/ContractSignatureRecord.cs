using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

/// <summary>Per-signer advanced electronic signature record.</summary>
public class ContractSignatureRecord
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid SignerId { get; set; }
    public DateTime SignedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string ContractHash { get; set; } = default!;
    public string SignatureToken { get; set; } = default!;
    public string ConsentText { get; set; } = default!;

    public Contract Contract { get; set; } = default!;
    public ApplicationUser Signer { get; set; } = default!;
}
