namespace NileChain.Application.Dtos.Payment;

public class PaymentMilestoneScheduleDto
{
    public Guid ContractId { get; set; }
    public decimal? ContractTotal { get; set; }
    public bool ContractTotalUnavailable { get; set; }
    public string? ContractTotalUnavailableReason { get; set; }

    /// <summary>Always present — this feature tracks status only, not settlement.</summary>
    public string Disclaimer { get; set; } =
        "Status tracking only — not a payment gateway.";

    public int? ScheduleGeneration { get; set; }
    public bool IsVoided { get; set; }
    public List<PaymentMilestoneDto> Milestones { get; set; } = new();
}

public class PaymentMilestoneDto
{
    public Guid TransactionId { get; set; }
    public int Sequence { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public decimal Percent { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = default!;
    public DateTime? PaidAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? VoidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<PaymentMilestoneEventDto> Events { get; set; } = new();
}

public class PaymentMilestoneEventDto
{
    public Guid EventId { get; set; }
    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = default!;
    public Guid ActorUserId { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
