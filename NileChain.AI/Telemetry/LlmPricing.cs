using Microsoft.Extensions.Configuration;

namespace NileChain.AI.Telemetry;

/// <summary>
/// Per-model token rates read from configuration
/// (<c>Llm:Pricing:&lt;model&gt;:InputPerMillionUsd</c> / <c>OutputPerMillionUsd</c>).
/// Nothing is hardcoded: an unpriced model yields a null cost rather than a made-up number.
/// </summary>
public sealed class LlmPricing
{
    public const string ConfigSection = "Llm:Pricing";

    private readonly Dictionary<string, ModelRate> _rates;

    public LlmPricing(IConfiguration configuration)
    {
        _rates = new Dictionary<string, ModelRate>(StringComparer.OrdinalIgnoreCase);

        var section = configuration.GetSection(ConfigSection);
        foreach (var model in section.GetChildren())
        {
            var input = model.GetValue<decimal?>("InputPerMillionUsd");
            var output = model.GetValue<decimal?>("OutputPerMillionUsd");
            if (input is null && output is null)
                continue;

            _rates[model.Key] = new ModelRate(input ?? 0m, output ?? 0m);
        }
    }

    private LlmPricing(Dictionary<string, ModelRate> rates) => _rates = rates;

    public static LlmPricing Empty() =>
        new(new Dictionary<string, ModelRate>(StringComparer.OrdinalIgnoreCase));

    public static LlmPricing ForTest(string model, decimal inputPerMillionUsd, decimal outputPerMillionUsd) =>
        new(new Dictionary<string, ModelRate>(StringComparer.OrdinalIgnoreCase)
        {
            [model] = new ModelRate(inputPerMillionUsd, outputPerMillionUsd)
        });

    public bool HasRateFor(string? model) =>
        model is not null && _rates.ContainsKey(model);

    /// <summary>Null when the model has no configured rate or no tokens were reported.</summary>
    public decimal? EstimateUsd(string? model, int? promptTokens, int? completionTokens)
    {
        if (model is null || !_rates.TryGetValue(model, out var rate))
            return null;

        if (promptTokens is null && completionTokens is null)
            return null;

        var cost = ((promptTokens ?? 0) / 1_000_000m) * rate.InputPerMillionUsd
                   + ((completionTokens ?? 0) / 1_000_000m) * rate.OutputPerMillionUsd;

        return decimal.Round(cost, 6);
    }

    private readonly record struct ModelRate(decimal InputPerMillionUsd, decimal OutputPerMillionUsd);
}
