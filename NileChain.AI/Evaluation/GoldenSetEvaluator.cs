using System.Text;
using NileChain.AI.Models;

namespace NileChain.AI.Evaluation;

public sealed record GoldenScenarioResult(string Name, string Intent, IReadOnlyList<string> Failures)
{
    public bool Passed => Failures.Count == 0;
}

public sealed record GoldenSetReport(IReadOnlyList<GoldenScenarioResult> Results)
{
    public bool Passed => Results.All(r => r.Passed);
    public int PassedCount => Results.Count(r => r.Passed);
    public int Total => Results.Count;

    public string Format()
    {
        var sb = new StringBuilder();
        foreach (var result in Results)
        {
            sb.AppendLine($"{(result.Passed ? "PASS" : "FAIL")} | {result.Name} | {result.Intent}");
            foreach (var failure in result.Failures)
                sb.AppendLine($"       - {failure}");
        }

        sb.AppendLine($"GOLDEN SET: {PassedCount}/{Total} passed");
        return sb.ToString();
    }
}

/// <summary>
/// Scores one agent response against a scenario's expectations. Kept free of any database or
/// provider so the same golden set can be graded from a unit test or a live smoke run.
/// </summary>
public static class GoldenSetEvaluator
{
    public static GoldenSetReport Evaluate(
        IReadOnlyList<(GoldenScenario Scenario, AgentResponse Response)> runs) =>
        new(runs.Select(r => Evaluate(r.Scenario, r.Response)).ToList());

    public static GoldenScenarioResult Evaluate(GoldenScenario scenario, AgentResponse response)
    {
        ArgumentNullException.ThrowIfNull(scenario);
        ArgumentNullException.ThrowIfNull(response);

        var expect = scenario.Expect;
        var failures = new List<string>();

        if (response.Success != expect.Success)
            failures.Add($"success: expected {expect.Success}, got {response.Success}");

        if (response.TopMatches.Count != expect.TopMatchCount)
            failures.Add($"topMatches: expected {expect.TopMatchCount}, got {response.TopMatches.Count}");

        if (response.TotalEligible != expect.TotalEligible)
            failures.Add($"totalEligible: expected {expect.TotalEligible}, got {response.TotalEligible}");

        if (response.TruncatedCount != expect.TruncatedCount)
            failures.Add($"truncatedCount: expected {expect.TruncatedCount}, got {response.TruncatedCount}");

        // The counts must be internally consistent, whatever the expectations say.
        if (response.TotalEligible != response.TopMatches.Count + response.TruncatedCount)
            failures.Add(
                $"counts do not add up: totalEligible={response.TotalEligible} "
                + $"but shown={response.TopMatches.Count} + truncated={response.TruncatedCount}");

        CheckGeography(response, expect, failures);
        CheckPeek(response, expect, failures);
        CheckTrail(response, expect, failures);

        if (expect.ExpectRiskWarning && response.RiskWarning is null)
            failures.Add("expected a risk warning, got none");
        if (!expect.ExpectRiskWarning && response.RiskWarning is not null)
            failures.Add($"unexpected risk warning: {response.RiskWarning.Message}");

        return new GoldenScenarioResult(scenario.Name, scenario.Intent, failures);
    }

    private static void CheckGeography(
        AgentResponse response,
        GoldenExpectation expect,
        List<string> failures)
    {
        var outsiders = response.TopMatches
            .Where(m => !expect.AllowedGovernorates.Contains(
                m.Governorate ?? string.Empty,
                StringComparer.OrdinalIgnoreCase))
            .Select(m => $"{m.FarmName} ({m.Governorate})")
            .ToList();

        if (outsiders.Count > 0)
        {
            failures.Add(
                "geographic scope breached by "
                + string.Join(", ", outsiders)
                + $"; allowed: {string.Join("/", expect.AllowedGovernorates)}");
        }
    }

    private static void CheckPeek(
        AgentResponse response,
        GoldenExpectation expect,
        List<string> failures)
    {
        var actual = response.PeekHint?.Governorate;

        if (expect.PeekGovernorate is null)
        {
            if (actual is not null)
                failures.Add($"unexpected peek hint for {actual}");
            return;
        }

        if (actual is null)
            failures.Add($"expected a peek hint for {expect.PeekGovernorate}, got none");
        else if (!string.Equals(actual, expect.PeekGovernorate, StringComparison.OrdinalIgnoreCase))
            failures.Add($"peek hint: expected {expect.PeekGovernorate}, got {actual}");
    }

    private static void CheckTrail(
        AgentResponse response,
        GoldenExpectation expect,
        List<string> failures)
    {
        var trail = response.ToolCallTrail ?? [];

        foreach (var required in expect.RequiredTrailFunctions)
        {
            if (!trail.Any(t => Mentions(t.FunctionName, required)))
                failures.Add($"trail is missing {required}");
        }

        foreach (var forbidden in expect.ForbiddenTrailFunctions)
        {
            // A blocked entry proves the guardrail fired, which is the opposite of a violation.
            if (trail.Any(t => Mentions(t.FunctionName, forbidden) && !t.Blocked))
                failures.Add($"trail contains a completed {forbidden} call");
        }
    }

    /// <summary>The deterministic path suffixes names, e.g. <c>SearchFarms(deterministic)</c>.</summary>
    private static bool Mentions(string? functionName, string tool) =>
        functionName is not null
        && functionName.Contains(tool, StringComparison.OrdinalIgnoreCase);
}
