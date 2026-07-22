using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

public class Transaction
{
    public Guid TransactionId { get; set; }
    public Guid ContractId { get; set; }
    public decimal Amount { get; set; }
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
    public string? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
}
