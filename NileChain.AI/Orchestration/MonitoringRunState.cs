using NileChain.AI.Models;

namespace NileChain.AI.Orchestration;

/// <summary>
/// Per-run memory for the proactive monitoring agent (tool-call trail + spam caps).
/// </summary>
public sealed class MonitoringRunState
{
    public const int MaxSendRiskAlertCalls = 20;
    public const int PriceShiftAlertThresholdPercent = 15;
    public const int WeatherDeliveryWindowDays = 14;
    public const int DefaultAlertDedupeHours = 24;

    public int SendRiskAlertCallCount { get; private set; }

    public List<ToolCallTrailEntry> Trail { get; } = new();

    public bool TryConsumeSendAlertSlot(out string? blockReason)
    {
        if (SendRiskAlertCallCount >= MaxSendRiskAlertCalls)
        {
            blockReason =
                $"SendRiskAlert capped at {MaxSendRiskAlertCalls} calls per monitoring run.";
            return false;
        }

        SendRiskAlertCallCount++;
        blockReason = null;
        return true;
    }

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
