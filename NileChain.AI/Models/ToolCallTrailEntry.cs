namespace NileChain.AI.Models;

/// <summary>
/// One tool invocation recorded during an agentic orchestrator run (evidence trail).
/// </summary>
public sealed class ToolCallTrailEntry
{
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public string FunctionName { get; init; } = string.Empty;
    public string ArgumentsSummary { get; init; } = string.Empty;
    public string ResultSummary { get; init; } = string.Empty;
    public bool Blocked { get; init; }
    public string? BlockReason { get; init; }
}

/// <summary>
/// Warning surfaced when a farm's risk score is below the safe threshold.
/// </summary>
public sealed class RiskWarningResult
{
    public Guid FarmId { get; init; }
    public int RiskScore { get; init; }
    public string Message { get; init; } = string.Empty;
    public bool RequiresFactoryConfirmation { get; init; } = true;
}
