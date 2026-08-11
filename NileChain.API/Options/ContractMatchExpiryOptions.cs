namespace NileChain.API.Options;

public sealed class ContractMatchExpiryOptions
{
    public const string SectionName = "ContractMatchExpiry";

    /// <summary>When false, the hosted service sleeps without expiring rows.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>How often to scan (hours). Default 1.</summary>
    public int IntervalHours { get; set; } = 1;

    /// <summary>
    /// Proposed matches and pending-signature contracts older than this many days expire.
    /// Default 14.
    /// </summary>
    public int ExpiryDays { get; set; } = 14;
}
