namespace NileChain.Domain.Entities;

public class AgentRun
{
    public Guid RunId { get; set; }
    public Guid RequestId { get; set; }
    public Guid? FactoryId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public bool Success { get; set; }
    public string? ErrorCode { get; set; }
    public int? TruncatedCount { get; set; }
    public string? OrchestratorMode { get; set; }

    public SupplyRequest? SupplyRequest { get; set; }
}
