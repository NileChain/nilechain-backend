using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using NileChain.AI.Sbg;

namespace NileChain.AI.Orchestration;

/// <summary>
/// Prompt-based tool loop for gateways (SBG) that lack OpenAI-style function calling.
/// The model emits JSON: {"tool":"Name","arguments":{...}} or {"done":true,"summary":"..."}.
/// </summary>
public static class SbgReactOrchestrator
{
    private const int MaxSteps = 12;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<string> RunAsync(
        IChatCompletionService chat,
        Kernel kernel,
        string systemPrompt,
        string userGoal,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        var toolGuide = """

            You MUST drive the workflow by calling tools via JSON only.
            On every turn reply with EXACTLY one JSON object (no markdown fences):

            To call a tool:
            {"tool":"SearchFarms","arguments":{"cropType":"Wheat","governorate":"Aswan","qualitySpecs":"Grade A","radiusKm":50}}

            Available tools and argument keys:
            - SearchFarms: cropType, governorate, qualitySpecs, radiusKm
            - WidenSearchRadius: (no arguments, use {})
            - CalculateRiskScore: farmId (GUID string)
            - FlagLowRiskWarning: farmId (GUID string), riskScore (int)
            - ProposeNextBestMatch: (no arguments, use {})
            - GenerateContract: farmId (GUID string)

            When the workflow is finished (or blocked waiting for factory confirmation):
            {"done":true,"summary":"short plain-text summary for the factory"}

            Never invent farm IDs or risk scores — only use values returned by tools.
            """;

        var history = new ChatHistory(systemPrompt + toolGuide);
        history.AddUserMessage(userGoal);

        string? finalSummary = null;

        for (var step = 0; step < MaxSteps; step++)
        {
            var response = await chat.GetChatMessageContentAsync(
                history,
                executionSettings: null,
                kernel: kernel,
                cancellationToken: cancellationToken);

            var content = response.Content?.Trim() ?? string.Empty;
            history.AddAssistantMessage(content);
            logger.LogInformation("SBG ReAct step {Step} model reply: {Reply}", step + 1, Truncate(content, 400));

            if (!TryParseAction(content, out var action, out var parseError))
            {
                history.AddUserMessage(
                    $"Your last reply was not valid JSON ({parseError}). " +
                    "Reply again with only a tool call JSON or {\"done\":true,\"summary\":\"...\"}.");
                continue;
            }

            if (action.Done)
            {
                finalSummary = action.Summary ?? "Done.";
                break;
            }

            if (string.IsNullOrWhiteSpace(action.Tool))
            {
                history.AddUserMessage(
                    "JSON must include \"tool\" or \"done\":true. Try again.");
                continue;
            }

            var plugin = kernel.Plugins["OrchestrationTools"];
            if (!plugin.TryGetFunction(action.Tool, out var function))
            {
                history.AddUserMessage(
                    $"Unknown tool '{action.Tool}'. Use one of: SearchFarms, WidenSearchRadius, " +
                    "CalculateRiskScore, FlagLowRiskWarning, ProposeNextBestMatch, GenerateContract.");
                continue;
            }

            var args = new KernelArguments();
            if (action.Arguments is { } argObj)
            {
                foreach (var prop in argObj.EnumerateObject())
                    args[prop.Name] = ConvertJsonValue(prop.Value);
            }

            string toolResult;
            try
            {
                var invoked = await kernel.InvokeAsync(function, args, cancellationToken);
                toolResult = invoked.ToString() ?? string.Empty;
            }
            catch (Exception ex)
            {
                toolResult = JsonSerializer.Serialize(new { error = ex.Message });
                logger.LogWarning(ex, "SBG ReAct tool {Tool} failed", action.Tool);
            }

            history.AddUserMessage(
                $"Tool {action.Tool} result:\n{toolResult}\n\n" +
                "Continue with the next tool JSON or {\"done\":true,\"summary\":\"...\"}.");
        }

        return finalSummary
               ?? "Orchestration finished without an explicit done summary.";
    }

    private static bool TryParseAction(string content, out ReactAction action, out string error)
    {
        action = new ReactAction();
        error = string.Empty;

        var json = ExtractJsonObject(content);
        if (json is null)
        {
            error = "no JSON object found";
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("done", out var doneEl)
                && doneEl.ValueKind is JsonValueKind.True or JsonValueKind.False
                && doneEl.GetBoolean())
            {
                action.Done = true;
                if (root.TryGetProperty("summary", out var summary))
                    action.Summary = summary.GetString();
                return true;
            }

            if (root.TryGetProperty("tool", out var toolEl))
                action.Tool = toolEl.GetString();

            if (root.TryGetProperty("arguments", out var argsEl)
                && argsEl.ValueKind == JsonValueKind.Object)
                action.Arguments = argsEl.Clone();
            else if (root.TryGetProperty("args", out var argsEl2)
                     && argsEl2.ValueKind == JsonValueKind.Object)
                action.Arguments = argsEl2.Clone();

            if (string.IsNullOrWhiteSpace(action.Tool) && !action.Done)
            {
                error = "missing tool";
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string? ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end > start)
                return trimmed[start..(end + 1)];
        }

        if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            return trimmed;

        var s = trimmed.IndexOf('{');
        var e = trimmed.LastIndexOf('}');
        if (s >= 0 && e > s)
            return trimmed[s..(e + 1)];

        return null;
    }

    private static object? ConvertJsonValue(JsonElement el) =>
        el.ValueKind switch
        {
            JsonValueKind.String => el.GetString(),
            JsonValueKind.Number when el.TryGetInt32(out var i) => i,
            JsonValueKind.Number when el.TryGetInt64(out var l) => l,
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => el.GetRawText()
        };

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private sealed class ReactAction
    {
        public bool Done { get; set; }
        public string? Summary { get; set; }
        public string? Tool { get; set; }
        public JsonElement? Arguments { get; set; }
    }
}
