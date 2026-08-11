using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

/// <summary>Append-only audit row for a payment-milestone status change (tracking only).</summary>
public class TransactionEvent
{
    public Guid EventId { get; set; }
    public Guid TransactionId { get; set; }
    public TransactionStatus? FromStatus { get; set; }
    public TransactionStatus ToStatus { get; set; }
    public Guid ActorUserId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Transaction Transaction { get; set; } = default!;
    public ApplicationUser ActorUser { get; set; } = default!;
}
