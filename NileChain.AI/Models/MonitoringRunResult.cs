namespace NileChain.AI.Models;

/// <summary>
/// Result of one proactive monitoring agent run (background or admin-triggered).
/// </summary>
public sealed class MonitoringRunResult
{
    public bool Success { get; init; }
    public string OrchestratorMode { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public int AlertsSent { get; init; }
    public int ActiveContractsReviewed { get; init; }
    public List<ToolCallTrailEntry> ToolCallTrail { get; init; } = new();
    public string? ErrorMessage { get; init; }
}
