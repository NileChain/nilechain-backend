namespace NileChain.Application.Dtos.Payment;

public class MockEscrowSessionDto
{
    public Guid EscrowTransactionId { get; set; }
    public Guid ContractId { get; set; }
    public Guid TransactionId { get; set; }
    public string MilestoneLabel { get; set; } = string.Empty;
    public decimal MilestoneAmountEgp { get; set; }
    public decimal PlatformFeePercent { get; set; }
    public decimal PlatformFeeEgp { get; set; }
    public decimal TotalChargedEgp { get; set; }
    public decimal FarmNetEgp { get; set; }
    public string Currency { get; set; } = "EGP";
    public string Status { get; set; } = default!;
    public string Gateway { get; set; } = "Mock";
    public string Disclaimer { get; set; } =
        "Demo mock payment — no real money is charged. NileChain simulates escrow + platform fee.";
}

public class EscrowTransactionDto
{
    public Guid EscrowTransactionId { get; set; }
    public Guid ContractId { get; set; }
    public Guid TransactionId { get; set; }
    public decimal MilestoneAmountEgp { get; set; }
    public decimal PlatformFeePercent { get; set; }
    public decimal PlatformFeeEgp { get; set; }
    public decimal TotalChargedEgp { get; set; }
    public decimal FarmNetEgp { get; set; }
    public string Currency { get; set; } = "EGP";
    public string Status { get; set; } = default!;
    public string Gateway { get; set; } = "Mock";
    public DateTime? HeldAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? RefundedAt { get; set; }
    public string? ReleaseReason { get; set; }
    public string? RefundReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateMockEscrowSessionRequest
{
    public Guid TransactionId { get; set; }
    public string? IdempotencyKey { get; set; }
}

public class AdminEscrowRefundRequest
{
    public string Reason { get; set; } = "Admin demo refund";
}
