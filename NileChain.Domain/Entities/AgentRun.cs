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

    /// <summary>Wall-clock duration of the whole run, including deterministic steps.</summary>
    public int? DurationMs { get; set; }

    /// <summary>Comma-separated providers actually used, including failover hops.</summary>
    public string? LlmProviders { get; set; }
    public string? LlmModels { get; set; }
    public int? LlmCalls { get; set; }

    /// <summary>Time spent waiting on the LLM. Null when no model call was made.</summary>
    public int? LlmLatencyMs { get; set; }

    /// <summary>Null when the provider does not report usage — never estimated.</summary>
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }

    /// <summary>Null unless the model has a configured price and reported tokens.</summary>
    public decimal? EstimatedCostUsd { get; set; }

    public SupplyRequest? SupplyRequest { get; set; }
}
