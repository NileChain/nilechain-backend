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

    /// <summary>Knowledge-base sources the reply actually cited as [n]; empty when it cited none.</summary>
    public List<CopilotCitation> Citations { get; set; } = [];

    /// <summary>
    /// True when the knowledge base returned no passage for this question, so any
    /// knowledge-base claim in the reply would be ungrounded.
    /// </summary>
    public bool KnowledgeUnavailable { get; set; }
}

public sealed class CopilotCitation
{
    public int Index { get; set; }
    public string Id { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Excerpt { get; set; } = string.Empty;
}
