using NileChain.AI.Models;

namespace NileChain.AI.Orchestration;

/// <summary>
/// Per-request memory for one orchestrator agentic loop.
/// Tracks tool-call caps, rejected farms, risk warnings, and the decision trail.
/// </summary>
public sealed class OrchestrationRunState
{
    public const int DefaultRadiusKm = 50;
    public const int RadiusStepKm = 50;
    public const int MaxRadiusKm = 300;
    public const int MaxWidenCalls = 1;
    public const int MaxProposeNextCalls = 1;
    public const int LowRiskThreshold = 40;

    public Guid RequestId { get; set; }
    public AgentRequest Request { get; set; } = new();

    public int CurrentRadiusKm { get; set; } = DefaultRadiusKm;
    public int WidenCallCount { get; set; }
    public int ProposeNextCallCount { get; set; }

    public HashSet<Guid> RejectedFarmIds { get; } = new();
    public List<MatchResult> LastSearchResults { get; set; } = new();
    public List<MatchResult> RankedCandidates { get; set; } = new();

    public bool RiskWarningActive { get; set; }
    public Guid? WarnedFarmId { get; set; }
    public int? WarnedRiskScore { get; set; }
    public bool FactoryConfirmedHighRisk { get; set; }

    public bool PartialResult { get; set; }
    public string? PartialReason { get; set; }

    public string? ContractDraft { get; set; }
    public bool ContractIncomplete { get; set; }
    public string? ContractValidationError { get; set; }

    public string? FactoryName { get; set; }

    public List<ToolCallTrailEntry> Trail { get; } = new();

    public void RecordTrail(
        string functionName,
        string argsSummary,
        string resultSummary,
        bool blocked = false,
        string? blockReason = null)
    {
        Trail.Add(new ToolCallTrailEntry
        {
            TimestampUtc = DateTime.UtcNow,
            FunctionName = functionName,
            ArgumentsSummary = argsSummary,
            ResultSummary = resultSummary,
            Blocked = blocked,
            BlockReason = blockReason
        });
    }
}
