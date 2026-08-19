using System.Text.Json;

namespace NileChain.AI.Telemetry;

/// <summary>
/// Best-effort token usage extraction from a provider response body.
/// The gateways in play front several model families whose bodies differ
/// (Bedrock <c>input_tokens</c>, OpenAI-style <c>prompt_tokens</c>), and some report
/// nothing at all — in which case both values stay null rather than being guessed.
/// </summary>
public static class LlmUsageJson
{
    public static (int? PromptTokens, int? CompletionTokens) TryRead(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(json);
            foreach (var container in UsageContainers(doc.RootElement))
            {
                var prompt = TryGetInt(container, "input_tokens", "prompt_tokens", "inputTokens", "promptTokens");
                var completion = TryGetInt(container, "output_tokens", "completion_tokens", "outputTokens", "completionTokens");

                if (prompt is not null || completion is not null)
                    return (prompt, completion);
            }
        }
        catch (JsonException)
        {
            // Usage is telemetry, never a reason to fail the call.
        }

        return (null, null);
    }

    private static IEnumerable<JsonElement> UsageContainers(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        foreach (var name in new[] { "usage", "usageMetadata", "token_usage" })
        {
            if (root.TryGetProperty(name, out var usage) && usage.ValueKind == JsonValueKind.Object)
                yield return usage;
        }

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object)
        {
            foreach (var nested in UsageContainers(data))
                yield return nested;
        }

        // Bedrock invoke responses put counts at the top level.
        yield return root;
    }

    private static int? TryGetInt(JsonElement el, params string[] names)
    {
        if (el.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var name in names)
        {
            if (el.TryGetProperty(name, out var prop)
                && prop.ValueKind == JsonValueKind.Number
                && prop.TryGetInt32(out var value))
            {
                return value;
            }
        }

        return null;
    }
}
