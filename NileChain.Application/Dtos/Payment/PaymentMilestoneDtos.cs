namespace NileChain.Application.Dtos.Payment;

public class PaymentMilestoneScheduleDto
{
    public Guid ContractId { get; set; }
    public decimal? ContractTotal { get; set; }
    public bool ContractTotalUnavailable { get; set; }
    public string? ContractTotalUnavailableReason { get; set; }

    /// <summary>Always present — clarifies mock gateway vs offline status tracking.</summary>
    public string Disclaimer { get; set; } =
        "Status tracking only — not a payment gateway.";

    /// <summary>When true, factory should use Pay (Demo); offline mark-paid is rejected.</summary>
    public bool MockGatewayEnabled { get; set; }

    /// <summary>When true, pay debits platform wallet (top-up via Paymob).</summary>
    public bool WalletEnabled { get; set; }

    /// <summary>Configured platform take-rate percent (CEO monetization).</summary>
    public decimal PlatformFeePercent { get; set; }

    /// <summary>Farm bank details for off-platform transfer (signed contracts only).</summary>
    public FarmPayoutDetailsDto? FarmPayoutDetails { get; set; }

    public int? ScheduleGeneration { get; set; }
    public bool IsVoided { get; set; }
    public bool PaymentsFrozenByDispute { get; set; }
    public List<PaymentMilestoneDto> Milestones { get; set; } = new();
    public List<EscrowTransactionDto> Escrows { get; set; } = new();
}

public class FarmPayoutDetailsDto
{
    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }
    public string? AccountMasked { get; set; }
    public string? Iban { get; set; }
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
    public DateTime? DueDate { get; set; }
    public bool IsOverdue { get; set; }
    public DateTime? VoidedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ReceiptUrl { get; set; }
    public string? ReceiptFileName { get; set; }
    public DateTime? ReceiptUploadedAt { get; set; }
    public Guid? ActiveEscrowTransactionId { get; set; }
    public string? EscrowStatus { get; set; }
    public decimal? PlatformFeeEgp { get; set; }
    public decimal? TotalChargedEgp { get; set; }
    public decimal? FarmNetEgp { get; set; }
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
