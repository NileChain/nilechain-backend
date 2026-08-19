namespace NileChain.Application.Dtos.Wallet;

public sealed class AdminWithdrawalListDto
{
    public int TotalCount { get; set; }
    public List<AdminWithdrawalDto> Items { get; set; } = [];
}

public sealed class AdminWithdrawalDto
{
    public Guid WithdrawalId { get; set; }
    public Guid WalletId { get; set; }
    public Guid UserId { get; set; }
    public string OwnerType { get; set; } = string.Empty;
    public Guid OwnerId { get; set; }
    public decimal AmountEgp { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? DestinationSummary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class AdminWithdrawalRejectRequest
{
    public string? Reason { get; set; }
}
