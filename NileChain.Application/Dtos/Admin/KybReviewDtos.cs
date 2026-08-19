namespace NileChain.Application.Dtos.Admin;

public sealed class KybDecisionRequest
{
    public string? Reason { get; set; }
}

public sealed class AdminOpsBadgesDto
{
    public int PendingVerifications { get; init; }
    public int OpenDisputes { get; init; }
    public int PendingWithdrawals { get; init; }
}

public sealed class FactoryHygieneDto
{
    public Guid FactoryId { get; set; }
    public string FactoryName { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public bool KybIncomplete { get; set; }
    public List<string> MissingKybKinds { get; set; } = [];
    public List<FarmHygieneDocumentDto> Documents { get; set; } = [];
}
