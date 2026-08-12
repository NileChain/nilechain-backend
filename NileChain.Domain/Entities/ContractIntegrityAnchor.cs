using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>
/// Append-only integrity record for a fully signed contract. Linked via PreviousHash into a platform chain.
/// </summary>
public class ContractIntegrityAnchor
{
    public Guid AnchorId { get; set; }
    public Guid ContractId { get; set; }
    public string ContentHash { get; set; } = default!;
    public string PreviousHash { get; set; } = default!;
    public long ChainIndex { get; set; }
    public string TxRef { get; set; } = default!;
    public ContractIntegrityAnchorStatus Status { get; set; } = ContractIntegrityAnchorStatus.Active;
    public DateTime AnchoredAtUtc { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
}
