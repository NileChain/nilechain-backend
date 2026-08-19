namespace NileChain.Application.Dtos.Signing;

public class SigningOtpResponse
{
    public DateTime ExpiresAt { get; set; }
}

public class ApproveContractRequest
{
    public string OtpCode { get; set; } = string.Empty;
    public string ConsentText { get; set; } = string.Empty;
}

public class ContractVerificationDto
{
    public bool IsValid { get; set; }
    public DateTime? SignedAt { get; set; }
    public string? SignerName { get; set; }
    public string? ContractHash { get; set; }
    public bool HashMatchesCurrentContent { get; set; }
    public List<ContractAuditTrailItemDto> AuditTrail { get; set; } = [];
}

public class ContractAuditTrailItemDto
{
    public string Action { get; set; } = default!;
    public Guid ActorId { get; set; }
    public DateTime Timestamp { get; set; }
    public string StateHash { get; set; } = default!;
}
