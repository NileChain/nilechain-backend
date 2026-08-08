using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using NileChain.AI.Models;
using NileChain.AI.Orchestration;
using NileChain.AI.Plugins;
using NileChain.AI.Sbg;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Agents;

/// <summary>
/// Agentic orchestrator: the LLM chooses which KernelFunctions to call (SearchFarms,
/// CalculateRiskScore, WidenSearchRadius, ProposeNextBestMatch, FlagLowRiskWarning,
/// GenerateContract) via Semantic Kernel automatic function calling.
/// Falls back to the legacy fixed Matching→Risk sequence when OpenAI is unavailable.
/// </summary>
public class OrchestratorAgent
{
    private const decimal RiskMatchContributionMax = 20m;

    private readonly MatchingAgent _matchingAgent;
    private readonly RiskAgent _riskAgent;
    private readonly MatchingPlugin _matchingPlugin;
    private readonly RiskPlugin _riskPlugin;
    private readonly Lazy<ContractAgent> _contractAgent;
    private readonly OpenAiKernelProvider _kernelProvider;
    private readonly SbgStudentChatClient _sbgClient;
    private readonly IConfiguration _configuration;
    private readonly NileChainDbContext _context;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(
        MatchingAgent matchingAgent,
        RiskAgent riskAgent,
        MatchingPlugin matchingPlugin,
        RiskPlugin riskPlugin,
        Lazy<ContractAgent> contractAgent,
        OpenAiKernelProvider kernelProvider,
        SbgStudentChatClient sbgClient,
        IConfiguration configuration,
        NileChainDbContext context,
        ILogger<OrchestratorAgent> logger)
    {
        _matchingAgent = matchingAgent;
        _riskAgent = riskAgent;
        _matchingPlugin = matchingPlugin;
        _riskPlugin = riskPlugin;
        _contractAgent = contractAgent;
        _kernelProvider = kernelProvider;
        _sbgClient = sbgClient;
        _configuration = configuration;
        _context = context;
        _logger = logger;
    }

    public async Task<AgentResponse> RunAsync(AgentRequest request)
    {
        try
        {
            if (_kernelProvider.IsAvailable)
                return await RunAgenticAsync(request);

            _logger.LogWarning(
                "OpenAI unavailable ({Reason}); using deterministic Matching→Risk fallback for RequestId {RequestId}",
                _kernelProvider.UnavailableReason,
                request.RequestId);

            return await RunDeterministicFallbackAsync(request);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Orchestrator failed for RequestId {RequestId}", request.RequestId);
            return new AgentResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                OrchestratorMode = "Error"
            };
        }
    }

    private async Task<AgentResponse> RunAgenticAsync(AgentRequest request)
    {
        var sw = Stopwatch.StartNew();
        var state = new OrchestrationRunState
        {
            RequestId = request.RequestId,
            Request = request,
            FactoryConfirmedHighRisk = request.ConfirmHighRiskWarning,
            FactoryName = await ResolveFactoryNameAsync(request.RequestId)
        };

        var tools = new OrchestrationToolsPlugin(
            _matchingPlugin,
            _riskPlugin,
            _contractAgent,
            _context,
            _logger,
            state);

        var kernel = CreatePerRequestKernel(tools);
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var systemPrompt = """
            You are the NileChain supply-matching orchestrator agent.
            You have tools and must use them — do not invent farms or risk scores.

            Goal workflow (adapt based on tool results):
            1. Call SearchFarms with radiusKm=50.
            2. If fewer than 3 farms, call WidenSearchRadius ONCE, then SearchFarms with the new radius.
            3. Call CalculateRiskScore for the top farms (at least the best one).
            4. If any score is < 40, call FlagLowRiskWarning and STOP contracting until the factory confirms.
            5. Do NOT call GenerateContract unless a match is clearly selected and risk is acceptable
               (or ConfirmHighRiskWarning was already true on the request).
            6. ProposeNextBestMatch may be used at most ONCE if a farm is rejected.
            7. When done, briefly summarize the ranked farms and any warnings in plain text.

            Hard rules:
            - WidenSearchRadius at most once.
            - ProposeNextBestMatch at most once.
            - Never call GenerateContract immediately after FlagLowRiskWarning without confirmation.
            """;

        var userGoal =
            $"""
            Factory supply request {request.RequestId}:
            - Crop: {request.CropType}
            - Quantity: {request.QuantityTons} tons
            - Quality: {request.QualitySpecs}
            - Price/ton: {request.PricePerTon} EGP
            - Delivery: {request.DeliveryDate:yyyy-MM-dd}
            - Governorate: {request.FactoryGovernorate}
            - ConfirmHighRiskWarning: {request.ConfirmHighRiskWarning}

            Find qualified farms, assess risk, widen search only if needed, flag low risk before contracting,
            and prepare the best candidate. Return a short final summary after tools finish.
            """;

        _logger.LogInformation(
            "Agentic orchestrator starting for RequestId {RequestId} provider={Provider} nativeTools={Native} (ConfirmHighRisk={Confirm})",
            request.RequestId,
            _kernelProvider.ProviderName,
            _kernelProvider.SupportsNativeToolCalling,
            request.ConfirmHighRiskWarning);

        string? finalContent;
        if (_kernelProvider.SupportsNativeToolCalling)
        {
            var history = new ChatHistory(systemPrompt);
            history.AddUserMessage(userGoal);

            // SK 1.78+: FunctionChoiceBehavior.Auto (replaces older ToolCallBehavior.AutoInvokeKernelFunctions).
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

            var finalMessage = await chat.GetChatMessageContentAsync(history, settings, kernel);
            finalContent = finalMessage?.Content;
        }
        else
        {
            // SBG (and similar) — JSON ReAct tool loop; no OpenAI tool_calls.
            finalContent = await SbgReactOrchestrator.RunAsync(
                chat,
                kernel,
                systemPrompt,
                userGoal,
                _logger);
        }

        sw.Stop();

        // Persist ranked matches (same as legacy path).
        var topMatches = state.RankedCandidates
            .OrderByDescending(m => m.MatchScore)
            .ThenByDescending(m => m.RiskScore)
            .ThenByDescending(m => m.IsVerified)
            .Take(5)
            .ToList();

        if (topMatches.Count == 0 && state.LastSearchResults.Count > 0)
            topMatches = state.LastSearchResults.Take(5).ToList();

        await PersistFarmMatchesAsync(request.RequestId, topMatches);

        LogFullTrail(state, sw.Elapsed, finalContent);

        var mode = _kernelProvider.SupportsNativeToolCalling
            ? "Agentic"
            : "AgenticSbgReact";

        var success = topMatches.Count > 0 || !string.IsNullOrWhiteSpace(state.ContractDraft);
        return new AgentResponse
        {
            Success = success,
            TopMatches = topMatches,
            ComparisonReport = finalContent ?? string.Empty,
            ContractDraft = state.ContractDraft ?? string.Empty,
            PartialResult = state.PartialResult,
            PartialReason = state.PartialReason,
            OrchestratorMode = mode,
            RiskWarning = state.RiskWarningActive
                ? new RiskWarningResult
                {
                    FarmId = state.WarnedFarmId ?? Guid.Empty,
                    RiskScore = state.WarnedRiskScore ?? 0,
                    Message = state.Trail
                        .LastOrDefault(t => t.FunctionName == "FlagLowRiskWarning")
                        ?.ResultSummary ?? "Low risk warning active",
                    RequiresFactoryConfirmation = !state.FactoryConfirmedHighRisk
                }
                : null,
            ContractIncomplete = state.ContractIncomplete,
            ContractValidationError = state.ContractValidationError,
            ToolCallTrail = state.Trail,
            ErrorMessage = success
                ? string.Empty
                : (state.PartialReason ?? "No matching farms found")
        };
    }

    private async Task<AgentResponse> RunDeterministicFallbackAsync(AgentRequest request)
    {
        var trail = new List<ToolCallTrailEntry>();
        void Trail(string name, string args, string result) =>
            trail.Add(new ToolCallTrailEntry
            {
                FunctionName = name,
                ArgumentsSummary = args,
                ResultSummary = result,
                TimestampUtc = DateTime.UtcNow
            });

        var matches = await _matchingAgent.RunAsync(request);
        Trail("SearchFarms(deterministic)", $"requestId={request.RequestId}", $"count={matches.Count}");

        if (matches.Count == 0)
        {
            return new AgentResponse
            {
                Success = false,
                ErrorMessage = "No matching farms found",
                OrchestratorMode = "DeterministicFallback",
                ToolCallTrail = trail
            };
        }

        var riskReports = await _riskAgent.RunAsync(matches);
        foreach (var match in matches)
        {
            var report = riskReports.FirstOrDefault(r => r.FarmId == match.FarmId);
            if (report is null)
                continue;

            match.RiskReport = report;
            Trail("CalculateRiskScore(deterministic)", $"farmId={match.FarmId}",
                $"score={report.OverallScore:0}; level={report.RiskLevel}");

            if (string.IsNullOrWhiteSpace(report.RiskLevel)
                && !string.IsNullOrWhiteSpace(report.AIAnalysis))
                continue;

            var previousRiskContribution = (match.RiskScore / 100m) * RiskMatchContributionMax;
            var updatedRiskContribution = (report.OverallScore / 100m) * RiskMatchContributionMax;
            match.MatchScore = match.MatchScore - previousRiskContribution + updatedRiskContribution;
            match.RiskScore = report.OverallScore;
            match.RiskLevel = report.RiskLevel;
        }

        matches = matches
            .OrderByDescending(m => m.MatchScore)
            .ThenByDescending(m => m.RiskScore)
            .ThenByDescending(m => m.IsVerified)
            .ToList();

        RiskWarningResult? warning = null;
        var top = matches.FirstOrDefault();
        if (top is not null && top.RiskScore < OrchestrationRunState.LowRiskThreshold)
        {
            warning = new RiskWarningResult
            {
                FarmId = top.FarmId,
                RiskScore = (int)top.RiskScore,
                Message =
                    $"HIGH RISK WARNING: top farm scored {top.RiskScore:0}/100. " +
                    "Contract generation should wait for ConfirmHighRiskWarning.",
                RequiresFactoryConfirmation = !request.ConfirmHighRiskWarning
            };
            Trail("FlagLowRiskWarning(deterministic)",
                $"farmId={top.FarmId}; riskScore={(int)top.RiskScore}",
                warning.Message);
        }

        await PersistFarmMatchesAsync(request.RequestId, matches);

        return new AgentResponse
        {
            Success = true,
            TopMatches = matches,
            OrchestratorMode = "DeterministicFallback",
            RiskWarning = warning,
            ToolCallTrail = trail
        };
    }

    private Kernel CreatePerRequestKernel(OrchestrationToolsPlugin tools)
    {
        // Fresh kernel per request so plugins/filters are not shared across concurrent runs.
        var kernel = LlmKernelFactory.CreateKernel(
                         _configuration,
                         out _,
                         out _,
                         out _,
                         _sbgClient)
                     ?? throw new InvalidOperationException(
                         "LLM is not configured (set SBG_BASE_URL + SBG_API_KEY, or OpenAI key/endpoint).");

        kernel.Plugins.AddFromObject(tools, pluginName: "OrchestrationTools");
        kernel.FunctionInvocationFilters.Add(
            new OrchestrationGuardrailFilter(tools.State, _logger));

        return kernel;
    }

    private async Task<string?> ResolveFactoryNameAsync(Guid requestId)
    {
        return await _context.SupplyRequests
            .AsNoTracking()
            .Where(r => r.RequestId == requestId)
            .Select(r => r.Factory!.Name)
            .FirstOrDefaultAsync();
    }

    private void LogFullTrail(OrchestrationRunState state, TimeSpan elapsed, string? finalSummary)
    {
        var sb = new StringBuilder();
        sb.AppendLine("======== AGENTIC TOOL-CALL TRAIL ========");
        sb.AppendLine($"RequestId: {state.RequestId}");
        sb.AppendLine($"Elapsed: {elapsed.TotalSeconds:0.00}s");
        sb.AppendLine($"PartialResult: {state.PartialResult} ({state.PartialReason})");
        sb.AppendLine($"WidenCalls: {state.WidenCallCount}/{OrchestrationRunState.MaxWidenCalls}");
        sb.AppendLine($"ProposeCalls: {state.ProposeNextCallCount}/{OrchestrationRunState.MaxProposeNextCalls}");
        sb.AppendLine($"RiskWarningActive: {state.RiskWarningActive}; Confirmed: {state.FactoryConfirmedHighRisk}");
        sb.AppendLine("--- steps ---");
        var i = 1;
        foreach (var step in state.Trail)
        {
            sb.AppendLine(
                $"{i++}. [{step.TimestampUtc:O}] {step.FunctionName}" +
                (step.Blocked ? " [BLOCKED]" : string.Empty));
            sb.AppendLine($"   args: {step.ArgumentsSummary}");
            sb.AppendLine($"   result: {step.ResultSummary}");
            if (step.BlockReason is not null)
                sb.AppendLine($"   block: {step.BlockReason}");
        }

        if (!string.IsNullOrWhiteSpace(finalSummary))
        {
            sb.AppendLine("--- final model summary ---");
            sb.AppendLine(finalSummary);
        }

        sb.AppendLine("======== END TRAIL ========");
        _logger.LogInformation("{Trail}", sb.ToString());
        Console.WriteLine(sb.ToString());
    }

    private async Task PersistFarmMatchesAsync(Guid requestId, List<MatchResult> matches)
    {
        if (matches.Count == 0)
            return;

        try
        {
            var farmIds = matches.Select(m => m.FarmId).ToList();

            var existingMatches = await _context.FarmMatches
                .Where(m => m.RequestId == requestId && farmIds.Contains(m.FarmId))
                .ToListAsync();

            var existingByFarmId = existingMatches
                .GroupBy(m => m.FarmId)
                .ToDictionary(g => g.Key, g => g.First());

            var persistedCount = 0;

            foreach (var match in matches)
            {
                if (existingByFarmId.TryGetValue(match.FarmId, out var existing))
                {
                    existing.MatchScore = match.MatchScore;
                    existing.RiskScore = match.RiskScore;
                    match.MatchId = existing.MatchId;
                    persistedCount++;
                    continue;
                }

                var matchId = Guid.NewGuid();
                _context.FarmMatches.Add(new FarmMatch
                {
                    MatchId = matchId,
                    RequestId = requestId,
                    FarmId = match.FarmId,
                    MatchScore = match.MatchScore,
                    RiskScore = match.RiskScore,
                    Status = FarmMatchStatus.Proposed,
                    CreatedAt = DateTime.UtcNow
                });
                match.MatchId = matchId;
                persistedCount++;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Persisted {Count} FarmMatch rows for RequestId {RequestId}",
                persistedCount,
                requestId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist FarmMatch rows for RequestId {RequestId}: {Exception}",
                requestId,
                ex.Message);
        }
    }
}

/// <summary>
/// Defense-in-depth filter: blocks GenerateContract when a low-risk warning is active
/// and the factory has not confirmed, even if the model tries to auto-invoke it.
/// </summary>
file sealed class OrchestrationGuardrailFilter : IFunctionInvocationFilter
{
    private readonly OrchestrationRunState _state;
    private readonly ILogger _logger;

    public OrchestrationGuardrailFilter(OrchestrationRunState state, ILogger logger)
    {
        _state = state;
        _logger = logger;
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var name = context.Function.Name;
        _logger.LogInformation(
            "SK invoking function {Function} with args {Args}",
            name,
            string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}")));

        if (string.Equals(name, "GenerateContract", StringComparison.OrdinalIgnoreCase)
            && _state.RiskWarningActive
            && !_state.FactoryConfirmedHighRisk)
        {
            var reason =
                "FILTER BLOCKED GenerateContract: FlagLowRiskWarning active without ConfirmHighRiskWarning.";
            _state.RecordTrail(
                "GenerateContract",
                string.Join(", ", context.Arguments.Select(a => $"{a.Key}={a.Value}")),
                reason,
                blocked: true,
                blockReason: reason);
            _logger.LogWarning("{Reason}", reason);
            context.Result = new FunctionResult(context.Function, new
            {
                blocked = true,
                reason
            });
            return;
        }

        await next(context);
    }
}
