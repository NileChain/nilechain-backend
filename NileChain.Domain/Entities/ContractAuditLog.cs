using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

/// <summary>
/// Append-only audit row. Application must not UPDATE or DELETE these records.
/// </summary>
public class ContractAuditLog
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public ContractAuditAction Action { get; set; }
    public Guid ActorId { get; set; }
    public string? IpAddress { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string StateHash { get; set; } = default!;

    public Contract Contract { get; set; } = default!;
    public ApplicationUser Actor { get; set; } = default!;
}
