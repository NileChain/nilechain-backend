using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using NileChain.AI.Models;
using NileChain.AI.Orchestration;
using NileChain.AI.Plugins;
using NileChain.AI.Sbg;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Agents;

/// <summary>
/// Background proactive monitoring agent: reviews active (Signed) contracts and decides
/// via tool-calling whether to alert farms/factories about weather or price-shift risk.
/// </summary>
public sealed class ProactiveMonitorAgent
{
    private const string MonitoringToolGuide = """

            You MUST drive the workflow by calling tools via JSON only.
            On every turn reply with EXACTLY one JSON object (no markdown fences):

            To call a tool:
            {"tool":"GetActiveContracts","arguments":{}}

            Available tools and argument keys:
            - GetActiveContracts: (no arguments)
            - CheckWeatherRisk: governorate (string), deliveryDate (ISO date string)
            - CheckMarketPriceShift: cropType (string), lockedPricePerTon (number)
            - HasRecentAlert: contractId (GUID string), alertType (WeatherRisk|PriceShift), withinHours (int, use 24)
            - SendRiskAlert: contractId (GUID), recipientUserId (GUID), alertType (WeatherRisk|PriceShift), message (string)

            When finished:
            {"done":true,"summary":"short plain-text summary of what was checked and which alerts were sent"}

            Never invent contract IDs or user IDs — only use values returned by tools.
            Do not exceed practical alert volume; skip contracts that already have recent alerts.
            """;

    private readonly OpenAiKernelProvider _kernelProvider;
    private readonly SbgStudentChatClient _sbgClient;
    private readonly IConfiguration _configuration;
    private readonly NileChainDbContext _db;
    private readonly ILogger<ProactiveMonitorAgent> _logger;

    public ProactiveMonitorAgent(
        OpenAiKernelProvider kernelProvider,
        SbgStudentChatClient sbgClient,
        IConfiguration configuration,
        NileChainDbContext db,
        ILogger<ProactiveMonitorAgent> logger)
    {
        _kernelProvider = kernelProvider;
        _sbgClient = sbgClient;
        _configuration = configuration;
        _db = db;
        _logger = logger;
    }

    public async Task<MonitoringRunResult> RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_kernelProvider.IsAvailable)
                return await RunAgenticAsync(cancellationToken);

            _logger.LogWarning(
                "LLM unavailable ({Reason}); proactive monitor using deterministic rule fallback",
                _kernelProvider.UnavailableReason);

            return await RunDeterministicFallbackAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ProactiveMonitorAgent failed");
            return new MonitoringRunResult
            {
                Success = false,
                OrchestratorMode = "Error",
                ErrorMessage = ex.Message,
                ToolCallTrail = new List<ToolCallTrailEntry>()
            };
        }
    }

    private async Task<MonitoringRunResult> RunAgenticAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var state = new MonitoringRunState();
        var tools = new MonitoringToolsPlugin(_db, _logger, state);
        var kernel = CreatePerRunKernel(tools);
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var systemPrompt = """
            You are the NileChain proactive contract monitoring agent.
            You watch active supply contracts and decide when farms and factories need risk alerts.
            Use only the provided tools — do not invent data.

            Goal:
            1. Call GetActiveContracts once.
            2. For each contract, decide whether to check weather risk if deliveryDate is within the next 14 days.
            3. For each contract, decide whether to check market price shift (locked vs latest market).
            4. If weather riskLevel is High, or abs(price percentageShift) >= 15:
               - Call HasRecentAlert for that contractId + alertType with withinHours=24.
               - If hasRecentAlert is false, SendRiskAlert to BOTH farmUserId and factoryUserId
                 (two calls) with alertType WeatherRisk or PriceShift and a clear message.
            5. Prefer reviewing the most urgent contracts first (soonest delivery / largest price gap).
            6. Stop after a thorough pass; do not spam. Max SendRiskAlert calls per run is 20.
            7. Finish with {"done":true,"summary":"..."}.
            """;

        var userGoal =
            $"""
            Run a full proactive monitoring pass now (UTC {DateTime.UtcNow:yyyy-MM-dd HH:mm}).
            Review active contracts, check weather/price risks where warranted, and notify both parties
            when thresholds are met and no similar alert was sent in the last 24 hours.
            """;

        _logger.LogInformation(
            "ProactiveMonitor starting provider={Provider} nativeTools={Native}",
            _kernelProvider.ProviderName,
            _kernelProvider.SupportsNativeToolCalling);

        string? finalContent;
        if (_kernelProvider.SupportsNativeToolCalling)
        {
            var history = new ChatHistory(systemPrompt);
            history.AddUserMessage(userGoal);
            var settings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0.2,
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                    autoInvoke: true,
                    options: new FunctionChoiceBehaviorOptions
                    {
                        AllowParallelCalls = false,
                        AllowConcurrentInvocation = false
                    })
            };
            var finalMessage = await chat.GetChatMessageContentAsync(
                history, settings, kernel, cancellationToken);
            finalContent = finalMessage?.Content;
        }
        else
        {
            finalContent = await SbgReactOrchestrator.RunAsync(
                chat,
                kernel,
                systemPrompt,
                userGoal,
                _logger,
                pluginName: "MonitoringTools",
                toolGuide: MonitoringToolGuide,
                knownToolsHint:
                    "GetActiveContracts, CheckWeatherRisk, CheckMarketPriceShift, " +
                    "HasRecentAlert, SendRiskAlert",
                maxSteps: 40,
                cancellationToken: cancellationToken);
        }

        sw.Stop();
        LogFullTrail(state, sw.Elapsed, finalContent);

        var alertsSent = state.Trail.Count(t =>
            t.FunctionName == "SendRiskAlert" && !t.Blocked
            && t.ResultSummary.Contains("sent=true", StringComparison.OrdinalIgnoreCase));

        var reviewed = CountContractsFromTrail(state);

        return new MonitoringRunResult
        {
            Success = true,
            OrchestratorMode = _kernelProvider.SupportsNativeToolCalling
                ? "Agentic"
                : "AgenticSbgReact",
            Summary = finalContent ?? string.Empty,
            AlertsSent = alertsSent,
            ActiveContractsReviewed = reviewed,
            ToolCallTrail = state.Trail
        };
    }

    private async Task<MonitoringRunResult> RunDeterministicFallbackAsync(
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var state = new MonitoringRunState();
        var tools = new MonitoringToolsPlugin(_db, _logger, state);

        var contractsJson = await tools.GetActiveContracts();
        using var doc = JsonDocument.Parse(contractsJson);
        var contracts = doc.RootElement.TryGetProperty("contracts", out var arr)
            ? arr.EnumerateArray().ToList()
            : new List<JsonElement>();

        var now = DateTime.UtcNow;
        var windowEnd = now.AddDays(MonitoringRunState.WeatherDeliveryWindowDays);

        foreach (var c in contracts)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var contractId = c.GetProperty("contractId").GetGuid();
            var farmUserId = c.GetProperty("farmUserId").GetGuid();
            var factoryUserId = c.GetProperty("factoryUserId").GetGuid();
            var cropType = c.GetProperty("cropType").GetString() ?? "Crop";
            var locked = c.TryGetProperty("lockedPricePerTon", out var lp) && lp.ValueKind != JsonValueKind.Null
                ? lp.GetDecimal()
                : 0m;
            var governorate = c.GetProperty("governorate").GetString() ?? "Unknown";
            DateTime? delivery = null;
            if (c.TryGetProperty("deliveryDate", out var dd) && dd.ValueKind != JsonValueKind.Null)
                delivery = dd.GetDateTime();

            if (delivery is { } d && d >= now && d <= windowEnd)
            {
                var weatherJson = await tools.CheckWeatherRisk(governorate, d);
                using var wDoc = JsonDocument.Parse(weatherJson);
                var level = wDoc.RootElement.TryGetProperty("riskLevel", out var rl)
                    ? rl.GetString()
                    : null;
                var reason = wDoc.RootElement.TryGetProperty("reason", out var rr)
                    ? rr.GetString()
                    : "Weather risk";

                if (string.Equals(level, "High", StringComparison.OrdinalIgnoreCase))
                {
                    await MaybeAlertBothAsync(
                        tools,
                        contractId,
                        farmUserId,
                        factoryUserId,
                        "WeatherRisk",
                        $"High weather risk for {cropType} delivery on {d:yyyy-MM-dd} in {governorate}. {reason}");
                }
            }

            if (locked > 0)
            {
                var priceJson = await tools.CheckMarketPriceShift(cropType, (double)locked);
                using var pDoc = JsonDocument.Parse(priceJson);
                if (pDoc.RootElement.TryGetProperty("percentageShift", out var shiftEl)
                    && shiftEl.ValueKind != JsonValueKind.Null
                    && Math.Abs(shiftEl.GetDecimal()) >= MonitoringRunState.PriceShiftAlertThresholdPercent)
                {
                    var shift = shiftEl.GetDecimal();
                    await MaybeAlertBothAsync(
                        tools,
                        contractId,
                        farmUserId,
                        factoryUserId,
                        "PriceShift",
                        $"Market price for {cropType} shifted {shift:0.##}% vs locked contract price {locked:0.##} EGP/ton.");
                }
            }
        }

        sw.Stop();
        var summary =
            $"Deterministic monitoring pass complete. contracts={contracts.Count}; " +
            $"alertsSent={state.SendRiskAlertCallCount}.";
        LogFullTrail(state, sw.Elapsed, summary);

        return new MonitoringRunResult
        {
            Success = true,
            OrchestratorMode = "DeterministicFallback",
            Summary = summary,
            AlertsSent = state.Trail.Count(t =>
                t.FunctionName == "SendRiskAlert" && !t.Blocked
                && t.ResultSummary.Contains("sent=true", StringComparison.OrdinalIgnoreCase)),
            ActiveContractsReviewed = contracts.Count,
            ToolCallTrail = state.Trail
        };
    }

    private static async Task MaybeAlertBothAsync(
        MonitoringToolsPlugin tools,
        Guid contractId,
        Guid farmUserId,
        Guid factoryUserId,
        string alertType,
        string message)
    {
        var recentJson = await tools.HasRecentAlert(contractId, alertType, 24);
        using var rDoc = JsonDocument.Parse(recentJson);
        if (rDoc.RootElement.TryGetProperty("hasRecentAlert", out var h)
            && h.ValueKind == JsonValueKind.True)
            return;

        await tools.SendRiskAlert(contractId, farmUserId, alertType, message);
        await tools.SendRiskAlert(contractId, factoryUserId, alertType, message);
    }

    private Kernel CreatePerRunKernel(MonitoringToolsPlugin tools)
    {
        var kernel = LlmKernelFactory.CreateKernel(
                         _configuration,
                         out _,
                         out _,
                         out _,
                         _sbgClient)
                     ?? throw new InvalidOperationException(
                         "LLM is not configured (set SBG_BASE_URL + SBG_API_KEY, or OpenAI key/endpoint).");

        kernel.Plugins.AddFromObject(tools, pluginName: "MonitoringTools");
        return kernel;
    }

    private static int CountContractsFromTrail(MonitoringRunState state)
    {
        var entry = state.Trail.LastOrDefault(t => t.FunctionName == "GetActiveContracts");
        if (entry is null)
            return 0;
        // ResultSummary like "activeContracts=N"
        var m = System.Text.RegularExpressions.Regex.Match(
            entry.ResultSummary, @"activeContracts=(\d+)");
        return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
    }

    private void LogFullTrail(MonitoringRunState state, TimeSpan elapsed, string? finalSummary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("======== PROACTIVE MONITOR TOOL-CALL TRAIL ========");
        sb.AppendLine($"Elapsed: {elapsed.TotalSeconds:0.00}s");
        sb.AppendLine($"SendRiskAlertCalls: {state.SendRiskAlertCallCount}/{MonitoringRunState.MaxSendRiskAlertCalls}");
        sb.AppendLine("--- steps ---");
        var i = 1;
        foreach (var step in state.Trail)
        {
            sb.AppendLine(
                $"{i++}. [{step.TimestampUtc:O}] {step.FunctionName}" +
                (step.Blocked ? " [BLOCKED]" : string.Empty));
            sb.AppendLine($"   args: {step.ArgumentsSummary}");
            sb.AppendLine($"   result: {step.ResultSummary}");
            if (step.Blocked)
                sb.AppendLine($"   blockReason: {step.BlockReason}");
        }
        sb.AppendLine("--- summary ---");
        sb.AppendLine(finalSummary ?? "(none)");
        sb.AppendLine("==================================================");
        _logger.LogInformation("{Trail}", sb.ToString());
    }
}
