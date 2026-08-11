namespace NileChain.AI.Models;

public class AgentResponse
{
    public bool Success { get; set; }
    public List<MatchResult> TopMatches { get; set; } = new();
    /// <summary>Eligible farms before Take-N truncation (when matching metadata is available).</summary>
    public int TotalEligible { get; set; }
    /// <summary>Farms excluded by the shortlist cap (TotalEligible - TopMatches.Count).</summary>
    public int TruncatedCount { get; set; }
    public string ComparisonReport { get; set; } = string.Empty;
    public string ContractDraft { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    /// <summary>Stable machine-readable error code for client mapping (never raw exception text).</summary>
    public string? ErrorCode { get; set; }

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
