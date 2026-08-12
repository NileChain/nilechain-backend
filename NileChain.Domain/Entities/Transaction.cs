using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>
/// Payment milestone status tracker for a signed contract.
/// Not a payment gateway record — no settlement or fund movement.
/// </summary>
public class Transaction
{
    public Guid TransactionId { get; set; }
    public Guid ContractId { get; set; }

    /// <summary>Schedule generation (increments after void-on-regen recreate).</summary>
    public int ScheduleGeneration { get; set; } = 1;

    /// <summary>1-based order within the generation.</summary>
    public int Sequence { get; set; }

    /// <summary>Display label (e.g. Deposit / On delivery).</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Stable key from config (e.g. Deposit). Not a card/gateway method.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>Percent of contract total this milestone represents.</summary>
    public decimal Percent { get; set; }

    /// <summary>Computed as contractTotal × Percent / 100 at schedule creation.</summary>
    public decimal Amount { get; set; }

    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>When the factory marked this milestone as paid (status only).</summary>
    public DateTime? PaidAt { get; set; }

    /// <summary>When the farm confirmed payment received (status only).</summary>
    public DateTime? ReceivedAt { get; set; }

    /// <summary>Expected payment date for overdue tracking (status only — not a gateway settlement date).</summary>
    public DateTime? DueDate { get; set; }

    /// <summary>Optional off-platform transfer receipt (Cloudinary URL).</summary>
    public string? ReceiptUrl { get; set; }
    public string? ReceiptPublicId { get; set; }
    public string? ReceiptFileName { get; set; }
    public DateTime? ReceiptUploadedAt { get; set; }

    public DateTime? VoidedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
    public ICollection<TransactionEvent> Events { get; set; } = new List<TransactionEvent>();
}
