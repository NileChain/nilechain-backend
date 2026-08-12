namespace NileChain.AI.Models;

public sealed class CopilotChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? PromptId { get; set; }
    public Guid? RequestId { get; set; }
    public Guid? MatchId { get; set; }
    public Guid? ContractId { get; set; }
    public Guid? FarmId { get; set; }
}

public sealed class CopilotChatResponse
{
    public bool Success { get; set; }
    public string Reply { get; set; } = string.Empty;
    public string? ErrorCode { get; set; }
    public bool UsedRag { get; set; }
    public string? Provider { get; set; }
}
