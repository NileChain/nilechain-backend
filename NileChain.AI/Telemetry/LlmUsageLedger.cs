namespace NileChain.AI.Telemetry;

public sealed record LlmCallRecord(
    string Provider,
    string Model,
    int? PromptTokens,
    int? CompletionTokens,
    long ElapsedMs);

public sealed record LlmUsageSummary(
    int Calls,
    string? Providers,
    string? Models,
    int? PromptTokens,
    int? CompletionTokens,
    long LlmLatencyMs,
    decimal? EstimatedCostUsd)
{
    public static LlmUsageSummary None { get; } =
        new(0, null, null, null, null, 0, null);
}

/// <summary>
/// Collects one entry per LLM call for the current scope so an agent run can report
/// which providers it actually used, how long they took, and what they cost.
/// Token counts stay null when the provider does not report usage — the run record
/// says "unknown" rather than guessing.
/// </summary>
public sealed class LlmUsageLedger
{
    private readonly List<LlmCallRecord> _calls = new();
    private readonly object _gate = new();

    public IReadOnlyList<LlmCallRecord> Calls
    {
        get
        {
            lock (_gate)
                return _calls.ToList();
        }
    }

    public void Record(
        string provider,
        string model,
        int? promptTokens,
        int? completionTokens,
        long elapsedMs)
    {
        lock (_gate)
        {
            _calls.Add(new LlmCallRecord(
                string.IsNullOrWhiteSpace(provider) ? "unknown" : provider,
                string.IsNullOrWhiteSpace(model) ? "unknown" : model,
                promptTokens,
                completionTokens,
                elapsedMs));
        }
    }

    public LlmUsageSummary Summarize(LlmPricing pricing)
    {
        ArgumentNullException.ThrowIfNull(pricing);

        List<LlmCallRecord> snapshot;
        lock (_gate)
            snapshot = _calls.ToList();

        if (snapshot.Count == 0)
            return LlmUsageSummary.None;

        var providers = Join(snapshot.Select(c => c.Provider));
        var models = Join(snapshot.Select(c => c.Model));
        var prompt = SumOrNull(snapshot.Select(c => c.PromptTokens));
        var completion = SumOrNull(snapshot.Select(c => c.CompletionTokens));
        var latency = snapshot.Sum(c => c.ElapsedMs);

        // Price per model, so a failover run that crossed providers is still costed correctly.
        decimal? cost = null;
        foreach (var group in snapshot.GroupBy(c => c.Model, StringComparer.OrdinalIgnoreCase))
        {
            var perModel = pricing.EstimateUsd(
                group.Key,
                SumOrNull(group.Select(c => c.PromptTokens)),
                SumOrNull(group.Select(c => c.CompletionTokens)));

            if (perModel is decimal value)
                cost = (cost ?? 0m) + value;
        }

        return new LlmUsageSummary(
            snapshot.Count,
            providers,
            models,
            prompt,
            completion,
            latency,
            cost is decimal total ? decimal.Round(total, 6) : null);
    }

    private static string Join(IEnumerable<string> values) =>
        string.Join(",", values.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => v));

    private static int? SumOrNull(IEnumerable<int?> values)
    {
        var reported = values.Where(v => v is not null).Select(v => v!.Value).ToList();
        return reported.Count == 0 ? null : reported.Sum();
    }
}
