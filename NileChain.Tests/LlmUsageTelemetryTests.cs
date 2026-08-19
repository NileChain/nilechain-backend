using NileChain.AI.Telemetry;

namespace NileChain.Tests;

public class LlmUsageTelemetryTests
{
    private const string Model = "amazon.nova-lite-v1:0";

    private static LlmPricing Pricing() =>
        LlmPricing.ForTest(Model, inputPerMillionUsd: 0.06m, outputPerMillionUsd: 0.24m);

    [Fact]
    public void Summarize_NoCalls_ReportsNothing()
    {
        var summary = new LlmUsageLedger().Summarize(Pricing());

        Assert.Equal(0, summary.Calls);
        Assert.Null(summary.Providers);
        Assert.Null(summary.PromptTokens);
        Assert.Null(summary.EstimatedCostUsd);
    }

    [Fact]
    public void Summarize_SumsTokensLatencyAndCost()
    {
        var ledger = new LlmUsageLedger();
        ledger.Record("sbg", Model, promptTokens: 1_000, completionTokens: 500, elapsedMs: 120);
        ledger.Record("sbg", Model, promptTokens: 2_000, completionTokens: 250, elapsedMs: 80);

        var summary = ledger.Summarize(Pricing());

        Assert.Equal(2, summary.Calls);
        Assert.Equal("sbg", summary.Providers);
        Assert.Equal(Model, summary.Models);
        Assert.Equal(3_000, summary.PromptTokens);
        Assert.Equal(750, summary.CompletionTokens);
        Assert.Equal(200, summary.LlmLatencyMs);

        // 3000 in @ $0.06/M + 750 out @ $0.24/M
        Assert.Equal(0.00036m, summary.EstimatedCostUsd);
    }

    [Fact]
    public void Summarize_FailoverAcrossProviders_ListsBothAndPricesPerModel()
    {
        var ledger = new LlmUsageLedger();
        ledger.Record("sbg", Model, 1_000, 0, 100);
        ledger.Record("OpenRouter", "openrouter/free", 5_000, 5_000, 200);

        var summary = ledger.Summarize(Pricing());

        Assert.Equal(2, summary.Calls);
        Assert.Equal("OpenRouter,sbg", summary.Providers);
        Assert.Contains("openrouter/free", summary.Models);

        // The unpriced model contributes nothing rather than inventing a rate.
        Assert.Equal(0.00006m, summary.EstimatedCostUsd);
    }

    [Fact]
    public void Summarize_UnpricedModelOnly_LeavesCostUnknown()
    {
        var ledger = new LlmUsageLedger();
        ledger.Record("OpenAI", "some-unlisted-model", 1_000, 1_000, 50);

        var summary = ledger.Summarize(Pricing());

        Assert.Equal(1, summary.Calls);
        Assert.Equal(2_000, summary.PromptTokens + summary.CompletionTokens);
        Assert.Null(summary.EstimatedCostUsd);
    }

    [Fact]
    public void Summarize_ProviderReportedNoTokens_KeepsTokensNull()
    {
        var ledger = new LlmUsageLedger();
        ledger.Record("sbg", Model, promptTokens: null, completionTokens: null, elapsedMs: 90);

        var summary = ledger.Summarize(Pricing());

        Assert.Equal(1, summary.Calls);
        Assert.Equal(90, summary.LlmLatencyMs);
        Assert.Null(summary.PromptTokens);
        Assert.Null(summary.EstimatedCostUsd);
    }

    [Fact]
    public void EstimateUsd_WithoutTokens_IsNull() =>
        Assert.Null(Pricing().EstimateUsd(Model, null, null));

    [Fact]
    public void EstimateUsd_UnknownModel_IsNull() =>
        Assert.Null(LlmPricing.Empty().EstimateUsd(Model, 1_000, 1_000));

    [Theory]
    [InlineData("""{"usage":{"prompt_tokens":12,"completion_tokens":34}}""", 12, 34)]
    [InlineData("""{"usage":{"input_tokens":7,"output_tokens":9}}""", 7, 9)]
    [InlineData("""{"data":{"usage":{"inputTokens":5,"outputTokens":6}}}""", 5, 6)]
    [InlineData("""{"input_tokens":3,"output_tokens":4}""", 3, 4)]
    public void TryRead_ParsesProviderShapes(string json, int prompt, int completion)
    {
        var (p, c) = LlmUsageJson.TryRead(json);

        Assert.Equal(prompt, p);
        Assert.Equal(completion, c);
    }

    [Theory]
    [InlineData("""{"content":"hello"}""")]
    [InlineData("not json at all")]
    [InlineData("")]
    [InlineData(null)]
    public void TryRead_NoUsageReported_ReturnsNulls(string? json)
    {
        var (p, c) = LlmUsageJson.TryRead(json);

        Assert.Null(p);
        Assert.Null(c);
    }
}
