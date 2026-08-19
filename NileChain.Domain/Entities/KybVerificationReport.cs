namespace NileChain.Domain.Entities;

public class KybVerificationReport
{
    public Guid ReportId { get; set; }

    /// <summary>
    /// Identity user ID that owns the KYB documents.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// 0..100 overall trust score computed by the agent.
    /// </summary>
    public int TrustScore { get; set; }

    /// <summary>Agent recommendation only — admin still decides.</summary>
    public string Recommendation { get; set; } = "NeedsReview";

    public string OverallSummary { get; set; } = string.Empty;

    /// <summary>
    /// Serialized JSON for the agent breakdown (per KYB kind).
    /// </summary>
    public string BreakdownJson { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

