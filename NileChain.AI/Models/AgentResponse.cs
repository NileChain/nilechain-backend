namespace NileChain.AI.Models;

public class AgentResponse
{
    public bool Success { get; set; }
    public List<MatchResult> TopMatches { get; set; } = new();
    public string ComparisonReport { get; set; } = string.Empty;
    public string ContractDraft { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;

    /// <summary>
    /// True when a hard tool-call cap stopped further retries (e.g. WidenSearchRadius).
    /// </summary>
    public bool PartialResult { get; set; }

    public string? PartialReason { get; set; }

    /// <summary>"Agentic" when LLM tool-calling ran; "DeterministicFallback" when OpenAI unavailable.</summary>
    public string OrchestratorMode { get; set; } = string.Empty;

    public RiskWarningResult? RiskWarning { get; set; }

    public bool ContractIncomplete { get; set; }

    public string? ContractValidationError { get; set; }

    /// <summary>Full tool-call decision trail for this run (write-up evidence).</summary>
    public List<ToolCallTrailEntry> ToolCallTrail { get; set; } = new();
}
