namespace NileChain.AI;

/// <summary>
/// Configuration for the farm-side proactive monitoring background agent.
/// </summary>
public sealed class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>When false, the hosted service skips runs (admin run-now still works). Default off for safer Production.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// Poll interval in minutes. Default 2 for demos; production would typically use ~1440 (daily).
    /// </summary>
    public int IntervalMinutes { get; set; } = 2;
}
