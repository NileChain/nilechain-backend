using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using NileChain.AI.Matching;
using NileChain.AI.Models;
using NileChain.AI.Orchestration;
using NileChain.AI.Plugins;
using NileChain.AI.Resilience;
using NileChain.AI.Sbg;
using NileChain.AI.Telemetry;
using NileChain.Application.Common;
using NileChain.Application.Notifications;
using NileChain.Domain.Common;
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

    /// <summary>
    /// Serializes parallel agent runs for the same supply request (race-safe FarmMatch upserts).
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> RequestRunGates = new();

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
    private readonly LlmUsageLedger _usage;
    private readonly LlmPricing _pricing;

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
        ILogger<OrchestratorAgent> logger,
        LlmUsageLedger usage,
        LlmPricing pricing)
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
        _usage = usage;
        _pricing = pricing;
    }

    public async Task<AgentResponse> RunAsync(AgentRequest request)
    {
        var gate = RequestRunGates.GetOrAdd(request.RequestId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync();
        var correlationId = Guid.NewGuid().ToString("N");
        var run = new AgentRun
        {
            RunId = Guid.NewGuid(),
            RequestId = request.RequestId,
            StartedAt = DateTime.UtcNow
        };

        try
        {
            var factoryId = await _context.SupplyRequests
                .AsNoTracking()
                .Where(r => r.RequestId == request.RequestId)
                .Select(r => (Guid?)r.FactoryId)
                .FirstOrDefaultAsync();
            run.FactoryId = factoryId;

            _context.AgentRuns.Add(run);
            await _context.SaveChangesAsync();

            AgentResponse response;
            if (!LlmCircuitBreaker.Shared.TryEnter(out var rejectReason))
            {
                _logger.LogWarning(
                    "LLM circuit open; falling back to deterministic Matching→Risk for RequestId {RequestId} CorrelationId={CorrelationId}",
                    request.RequestId,
                    correlationId);

                response = await RunDeterministicFallbackAsync(request);
                response.OrchestratorMode = string.IsNullOrWhiteSpace(response.OrchestratorMode)
                    ? "CircuitOpenFallback"
                    : response.OrchestratorMode;
                if (!response.Success)
                {
                    response.ErrorCode ??= ClientErrorSanitizer.ServiceUnavailableCode;
                    response.ErrorMessage = string.IsNullOrWhiteSpace(response.ErrorMessage)
                        ? (rejectReason ?? ClientErrorSanitizer.ServiceUnavailableMessage)
                        : response.ErrorMessage;
                }
            }
            else
            {
                try
                {
                    response = await RunUnlockedAsync(request);
                    if (response.Success || response.TopMatches.Count > 0)
                        LlmCircuitBreaker.Shared.RecordSuccess();
                    else if (string.Equals(response.OrchestratorMode, "Error", StringComparison.OrdinalIgnoreCase))
                        LlmCircuitBreaker.Shared.RecordFailure();
                    else
                        LlmCircuitBreaker.Shared.RecordSuccess();
                }
                catch (Exception)
                {
                    LlmCircuitBreaker.Shared.RecordFailure();
                    throw;
                }
            }

            response.ToolCallTrail = SanitizeTrail(response.ToolCallTrail);
            await CompleteAgentRunAsync(run, response);
            return response;
        }
        catch (Exception ex)
        {
            LlmCircuitBreaker.Shared.RecordFailure();
            _logger.LogError(
                ex,
                "Orchestrator failed for RequestId {RequestId} CorrelationId={CorrelationId}",
                request.RequestId,
                correlationId);

            var failed = new AgentResponse
            {
                Success = false,
                ErrorCode = ClientErrorSanitizer.AgentFailureCode,
                ErrorMessage = ClientErrorSanitizer.AgentFailureMessage,
                OrchestratorMode = "Error"
            };

            run.CompletedAt = DateTime.UtcNow;
            run.Success = false;
            run.ErrorCode = failed.ErrorCode;
            run.OrchestratorMode = failed.OrchestratorMode;
            // A failed run still burned tokens; record what it cost before returning.
            ApplyRunTelemetry(run);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (Exception persistEx)
            {
                _logger.LogWarning(persistEx, "Failed to persist AgentRun failure for {RunId}", run.RunId);
            }

            return failed;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task CompleteAgentRunAsync(AgentRun run, AgentResponse response)
    {
        run.CompletedAt = DateTime.UtcNow;
        run.Success = response.Success;
        run.ErrorCode = response.ErrorCode
            ?? (response.Success ? null : "AI.NoMatches");
        run.TruncatedCount = response.TruncatedCount;
        ApplyRunTelemetry(run);
        run.OrchestratorMode = response.OrchestratorMode;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to complete AgentRun {RunId}", run.RunId);
        }
    }

    private void ApplyRunTelemetry(AgentRun run)
    {
        var summary = _usage.Summarize(_pricing);

        run.DurationMs = (int)Math.Round(
            ((run.CompletedAt ?? DateTime.UtcNow) - run.StartedAt).TotalMilliseconds);

        if (summary.Calls == 0)
            return;

        run.LlmCalls = summary.Calls;
        run.LlmProviders = summary.Providers;
        run.LlmModels = summary.Models;
        run.LlmLatencyMs = (int)Math.Min(summary.LlmLatencyMs, int.MaxValue);
        run.PromptTokens = summary.PromptTokens;
        run.CompletionTokens = summary.CompletionTokens;
        run.EstimatedCostUsd = summary.EstimatedCostUsd;
    }

    private static List<ToolCallTrailEntry> SanitizeTrail(List<ToolCallTrailEntry> trail) =>
        trail.Select(t => new ToolCallTrailEntry
        {
            TimestampUtc = t.TimestampUtc,
            FunctionName = t.FunctionName,
            ArgumentsSummary = ClientErrorSanitizer.SanitizeTrailText(t.ArgumentsSummary),
            ResultSummary = ClientErrorSanitizer.SanitizeTrailText(t.ResultSummary),
            Blocked = t.Blocked,
            BlockReason = t.BlockReason is null
                ? null
                : ClientErrorSanitizer.SanitizeTrailText(t.BlockReason)
        }).ToList();

    private async Task<AgentResponse> RunUnlockedAsync(AgentRequest request)
    {
        try
        {
            var chain = LlmKernelFactory.ResolveProviderChain(_configuration);
            if (chain.Count > 0)
                return await RunAgenticWithFailoverAsync(request, chain);

            _logger.LogWarning(
                "LLM unavailable ({Reason}); using deterministic Matching→Risk fallback for RequestId {RequestId}",
                _kernelProvider.UnavailableReason ?? "no providers configured",
                request.RequestId);

            return await RunDeterministicFallbackAsync(request);
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString("N");
            _logger.LogError(
                ex,
                "Orchestrator failed for RequestId {RequestId} CorrelationId={CorrelationId}",
                request.RequestId,
                correlationId);
            return new AgentResponse
            {
                Success = false,
                ErrorCode = ClientErrorSanitizer.AgentFailureCode,
                ErrorMessage = ClientErrorSanitizer.AgentFailureMessage,
                OrchestratorMode = "Error"
            };
        }
    }

    private async Task<AgentResponse> RunAgenticWithFailoverAsync(
        AgentRequest request,
        IReadOnlyList<string> chain)
    {
        Exception? lastFailure = null;

        for (var i = 0; i < chain.Count; i++)
        {
            var providerKey = chain[i];
            try
            {
                _logger.LogInformation(
                    "Agentic run trying provider={Provider} ({Index}/{Total}) for RequestId {RequestId}",
                    LlmKernelFactory.DisplayName(providerKey),
                    i + 1,
                    chain.Count,
                    request.RequestId);

                return await RunAgenticAsync(request, providerKey);
            }
            catch (Exception ex) when (
                LlmKernelFactory.IsProviderFailure(ex) && i < chain.Count - 1)
            {
                lastFailure = ex;
                _logger.LogWarning(
                    ex,
                    "Provider {Provider} failed for RequestId {RequestId}; failing over to {Next}",
                    LlmKernelFactory.DisplayName(providerKey),
                    request.RequestId,
                    LlmKernelFactory.DisplayName(chain[i + 1]));
            }
        }

        throw lastFailure ?? new InvalidOperationException("No LLM provider succeeded.");
    }

    private async Task<AgentResponse> RunAgenticAsync(AgentRequest request, string providerKey)
    {
        var sw = Stopwatch.StartNew();
        var nativeTools = LlmKernelFactory.SupportsNativeToolCalling(providerKey);
        var providerDisplay = LlmKernelFactory.DisplayName(providerKey);
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

        var kernel = CreatePerRequestKernel(tools, providerKey);
        var chat = kernel.GetRequiredService<IChatCompletionService>();

        var systemPrompt = """
            You are the NileChain supply-matching orchestrator agent.
            You have tools and must use them — do not invent farms or risk scores.

            Goal workflow (adapt based on tool results):
            1. Call SearchFarms with radiusKm=50.
            2. Geographic expansion rules (HARD — tools enforce these too):
               - Exact: NEVER call WidenSearchRadius. Return 0–N Exact matches as-is.
               - Nearby: NEVER expand to Nationwide. Use Nearby results as-is.
               - Nationwide only: if fewer than 3 farms, you MAY call WidenSearchRadius ONCE, then SearchFarms again.
            3. Call CalculateRiskScore for the top farms (at least the best one).
            4. If any score is < 40, call FlagLowRiskWarning and STOP contracting until the factory confirms.
            5. Do NOT call GenerateContract unless a match is clearly selected and risk is acceptable
               (or ConfirmHighRiskWarning was already true on the request).
            6. ProposeNextBestMatch may be used at most ONCE if a farm is rejected.
            7. When done, briefly summarize the ranked farms and any warnings in plain text.

            Hard rules:
            - Exact means Exact — never invent or request farms outside the selected governorate(s).
            - WidenSearchRadius at most once, and only when scope is Nationwide.
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

            Find qualified farms within the persisted geographic scope, assess risk,
            widen search ONLY if the factory chose Nationwide and more candidates are needed,
            flag low risk before contracting, and prepare the best candidate.
            Return a short final summary after tools finish.
            """;

        _logger.LogInformation(
            "Agentic orchestrator starting for RequestId {RequestId} provider={Provider} nativeTools={Native} (ConfirmHighRisk={Confirm})",
            request.RequestId,
            providerDisplay,
            nativeTools,
            request.ConfirmHighRiskWarning);

        string? finalContent = null;
        Exception? chatFailure = null;
        try
        {
            if (nativeTools)
            {
                var history = new ChatHistory(systemPrompt);
                history.AddUserMessage(userGoal);

                // SK 1.78+: FunctionChoiceBehavior.Auto (replaces older ToolCallBehavior.AutoInvokeKernelFunctions).
                var settings = new OpenAIPromptExecutionSettings
                {
                    Temperature = 0.2,
                    // Keep completions small — free Groq TPM is tight for multi-tool agent runs.
                    MaxTokens = 1024,
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(
                        autoInvoke: true,
                        options: new FunctionChoiceBehaviorOptions
                        {
                            AllowParallelCalls = false,
                            AllowConcurrentInvocation = false
                        })
                };

                finalContent = await InvokeChatWithRateLimitRetryAsync(
                    () => chat.GetChatMessageContentAsync(history, settings, kernel));
            }
            else
            {
                // SBG (and similar) — JSON ReAct tool loop; no OpenAI tool_calls.
                finalContent = await InvokeChatWithRateLimitRetryAsync(
                    () => SbgReactOrchestrator.RunAsync(
                        chat,
                        kernel,
                        systemPrompt,
                        userGoal,
                        _logger));
            }
        }
        catch (Exception ex) when (HasToolProducedMatches(state))
        {
            // OpenAI SDK may crash parsing Refusal from OpenAI-compatible gateways after tools
            // already succeeded. Prefer returning ranked matches over failing the whole run.
            chatFailure = ex;
            _logger.LogWarning(
                ex,
                "LLM chat parse/provider failure after tools produced matches for RequestId {RequestId}; returning tool results",
                request.RequestId);
            finalContent =
                "Matching completed via tools. Final LLM summary unavailable "
                + "(provider response could not be parsed).";
        }

        sw.Stop();

        // Persist ranked matches (same as legacy path).
        var topMatches = state.RankedCandidates
            .OrderByDescending(m => m.MatchScore)
            .ThenByDescending(m => m.RiskScore)
            .ThenByDescending(m => m.IsVerified)
            .ThenBy(m => m.FarmId)
            .Take(MatchingLimits.DefaultMaxResults)
            .ToList();

        if (topMatches.Count == 0 && state.LastSearchResults.Count > 0)
            topMatches = state.LastSearchResults.Take(MatchingLimits.DefaultMaxResults).ToList();

        if (topMatches.Count == 0 && chatFailure is not null)
            throw chatFailure;

        var superseded = await PersistFarmMatchesAsync(request.RequestId, topMatches);
        state.LastSupersededCount = superseded;

        LogFullTrail(state, sw.Elapsed, finalContent);

        var mode = nativeTools ? "Agentic" : "AgenticSbgReact";

        var totalEligible = state.LastTotalEligible > 0
            ? state.LastTotalEligible
            : state.RankedCandidates.Count;
        var truncatedCount = state.LastTruncatedCount > 0
            ? state.LastTruncatedCount
            : Math.Max(0, totalEligible - topMatches.Count);

        var success = topMatches.Count > 0 || !string.IsNullOrWhiteSpace(state.ContractDraft);
        return new AgentResponse
        {
            Success = success,
            TopMatches = topMatches,
            TotalEligible = totalEligible,
            TruncatedCount = truncatedCount,
            SupersededCount = state.LastSupersededCount,
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
            PeekHint = state.PeekHint,
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

        var search = await _matchingAgent.RunAsync(request);
        var matches = search.Results;
        Trail("SearchFarms(deterministic)", $"requestId={request.RequestId}",
            $"count={matches.Count}; totalEligible={search.TotalEligible}; truncated={search.TruncatedCount}");

        if (matches.Count == 0)
        {
            return new AgentResponse
            {
                Success = false,
                ErrorMessage = "No matching farms found",
                TotalEligible = search.TotalEligible,
                TruncatedCount = search.TruncatedCount,
                OrchestratorMode = "DeterministicFallback",
                ToolCallTrail = trail,
                PeekHint = search.PeekHint
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
            .ThenBy(m => m.FarmId)
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

        var superseded = await PersistFarmMatchesAsync(request.RequestId, matches);

        return new AgentResponse
        {
            Success = true,
            TopMatches = matches,
            TotalEligible = search.TotalEligible,
            TruncatedCount = search.TruncatedCount,
            SupersededCount = superseded,
            OrchestratorMode = "DeterministicFallback",
            RiskWarning = warning,
            ToolCallTrail = trail,
            PeekHint = search.PeekHint
        };
    }

    private Kernel CreatePerRequestKernel(OrchestrationToolsPlugin tools, string providerKey)
    {
        // Fresh kernel per request so plugins/filters are not shared across concurrent runs.
        var kernel = LlmKernelFactory.CreateKernelForProvider(
                         providerKey,
                         _configuration,
                         out var unavailableReason,
                         out _,
                         out _,
                         _sbgClient,
                         _usage)
                     ?? throw new InvalidOperationException(
                         unavailableReason
                         ?? "LLM is not configured (set SBG_BASE_URL + SBG_API_KEY, or OpenAI/Groq key).");

        kernel.Plugins.AddFromObject(tools, pluginName: "OrchestrationTools");
        kernel.FunctionInvocationFilters.Add(
            new OrchestrationGuardrailFilter(tools.State, _logger));

        return kernel;
    }

    private static bool HasToolProducedMatches(OrchestrationRunState state) =>
        state.RankedCandidates.Count > 0
        || state.LastSearchResults.Count > 0
        || !string.IsNullOrWhiteSpace(state.ContractDraft);

    /// <summary>
    /// Free-tier providers (Groq) often return HTTP 429 TPM limits mid-agent-run.
    /// Wait and retry a few times instead of failing the whole orchestration.
    /// </summary>
    private async Task<string?> InvokeChatWithRateLimitRetryAsync(Func<Task<ChatMessageContent>> invoke)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var message = await invoke();
                return message?.Content;
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRateLimited(ex))
            {
                var delay = GetRateLimitDelay(ex, attempt);
                _logger.LogWarning(
                    ex,
                    "LLM rate-limited (attempt {Attempt}/{Max}); waiting {Delay}s then retrying",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }

        // Final attempt — let the exception surface to RunAsync.
        var last = await invoke();
        return last?.Content;
    }

    private async Task<string?> InvokeChatWithRateLimitRetryAsync(Func<Task<string>> invoke)
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await invoke();
            }
            catch (Exception ex) when (attempt < maxAttempts && IsRateLimited(ex))
            {
                var delay = GetRateLimitDelay(ex, attempt);
                _logger.LogWarning(
                    ex,
                    "LLM rate-limited (attempt {Attempt}/{Max}); waiting {Delay}s then retrying",
                    attempt,
                    maxAttempts,
                    delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }

        return await invoke();
    }

    private static bool IsRateLimited(Exception ex)
    {
        for (var cur = ex; cur is not null; cur = cur.InnerException)
        {
            var text = cur.Message ?? string.Empty;

            // Daily / long quotas → fail over immediately (don't wait 15+ minutes).
            if (text.Contains("tokens per day", StringComparison.OrdinalIgnoreCase)
                || text.Contains("TPD", StringComparison.OrdinalIgnoreCase)
                || text.Contains("Daily request limit", StringComparison.OrdinalIgnoreCase)
                || text.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase)
                || text.Contains("no credits", StringComparison.OrdinalIgnoreCase)
                || System.Text.RegularExpressions.Regex.IsMatch(
                    text,
                    @"try again in\s+\d+\s*m",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            {
                return false;
            }

            // Short TPM windows (e.g. "try again in 6.67s") → retry same provider.
            if (text.Contains("tokens per minute", StringComparison.OrdinalIgnoreCase)
                || text.Contains("try again in", StringComparison.OrdinalIgnoreCase)
                || (text.Contains("429", StringComparison.OrdinalIgnoreCase)
                    && text.Contains("rate_limit", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static TimeSpan GetRateLimitDelay(Exception ex, int attempt)
    {
        // Groq often says "Please try again in 6.67s".
        var text = ex.ToString();
        var match = System.Text.RegularExpressions.Regex.Match(
            text,
            @"try again in\s+([0-9]+(?:\.[0-9]+)?)\s*s",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success && double.TryParse(
                match.Groups[1].Value,
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture,
                out var seconds)
            && seconds > 0)
        {
            return TimeSpan.FromSeconds(Math.Clamp(seconds + 1.5, 3, 60));
        }

        // Exponential backoff fallback: 8s, 12s, 18s...
        return TimeSpan.FromSeconds(Math.Min(8 * attempt, 45));
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

    private async Task<int> PersistFarmMatchesAsync(Guid requestId, List<MatchResult> matches)
    {
        if (matches.Count == 0)
            return 0;

        try
        {
            return await UpsertFarmMatchesAsync(requestId, matches);
        }
        catch (DbUpdateException ex) when (UniqueConstraintViolation.IsViolation(ex))
        {
            _logger.LogWarning(
                ex,
                "FarmMatch unique conflict for RequestId {RequestId}; retrying upsert",
                requestId);

            DetachAddedFarmMatches();
            try
            {
                return await UpsertFarmMatchesAsync(requestId, matches);
            }
            catch (Exception retryEx)
            {
                _logger.LogError(
                    retryEx,
                    "Failed to persist FarmMatch rows for RequestId {RequestId} after unique conflict retry: {Exception}",
                    requestId,
                    retryEx.Message);
                return 0;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist FarmMatch rows for RequestId {RequestId}: {Exception}",
                requestId,
                ex.Message);
            return 0;
        }
    }

    private async Task<int> UpsertFarmMatchesAsync(Guid requestId, List<MatchResult> matches)
    {
        var farmIds = matches.Select(m => m.FarmId).ToHashSet();

        // Supersede stale Proposed/Countered matches that fell out of the new shortlist.
        // Never touch Accepted or factory-excluded rows.
        var staleMatches = await _context.FarmMatches
            .Include(m => m.Farm)
            .Include(m => m.SupplyRequest)
                .ThenInclude(r => r.Factory)
            .Where(m =>
                m.RequestId == requestId
                && !m.IsExcludedByFactory
                && (m.Status == FarmMatchStatus.Proposed || m.Status == FarmMatchStatus.Countered)
                && !farmIds.Contains(m.FarmId))
            .ToListAsync();

        foreach (var stale in staleMatches)
        {
            stale.Status = FarmMatchStatus.Expired;
            NotifyMatchSuperseded(stale.Farm?.UserId, stale.MatchId);
            var factoryUserId = stale.SupplyRequest?.Factory?.UserId;
            if (factoryUserId is Guid fUid && fUid != Guid.Empty
                && fUid != (stale.Farm?.UserId ?? Guid.Empty))
            {
                NotifyMatchSuperseded(fUid, stale.MatchId);
            }
        }

        var requestTerms = await _context.SupplyRequests
            .AsNoTracking()
            .Where(r => r.RequestId == requestId)
            .Select(r => new
            {
                r.CropTypeId,
                r.DeliveryPoint,
                r.FreightPayer,
                r.TransitRisk
            })
            .FirstOrDefaultAsync();
        var requestCropId = requestTerms?.CropTypeId ?? Guid.Empty;

        var snapshotFarms = await _context.Farm
            .AsNoTracking()
            .Where(f => farmIds.Contains(f.FarmId))
            .Select(f => new
            {
                f.FarmId,
                f.Governorate,
                f.RiskScore,
                f.IsVerified,
                Crops = f.FarmCrops.Select(c => new
                {
                    c.CropTypeId,
                    c.AvailableQuantityTons,
                    c.MinPricePerTon
                }).ToList()
            })
            .ToListAsync();
        var snapshotByFarm = snapshotFarms.ToDictionary(f => f.FarmId);

        var existingMatches = await _context.FarmMatches
            .Where(m => m.RequestId == requestId && farmIds.Contains(m.FarmId))
            .ToListAsync();

        var existingByFarmId = existingMatches
            .GroupBy(m => m.FarmId)
            .ToDictionary(g => g.Key, g => g.First());

        var persistedCount = 0;
        var newProposedFarmIds = new List<Guid>();

        foreach (var match in matches)
        {
            var govSnapshot = MatchGovernoratePolicy.SnapshotFromFarm(match.Governorate);
            string? eligibilityJson = null;
            if (snapshotByFarm.TryGetValue(match.FarmId, out var farmSnap))
            {
                var cropForRequest = farmSnap.Crops.FirstOrDefault(c => c.CropTypeId == requestCropId);
                eligibilityJson = new NileChain.Domain.Matching.MatchEligibilitySnapshot
                {
                    Governorate = farmSnap.Governorate,
                    CropTypeIds = farmSnap.Crops.Select(c => c.CropTypeId).ToList(),
                    AvailableQuantityTons = cropForRequest?.AvailableQuantityTons,
                    MinPricePerTon = cropForRequest?.MinPricePerTon,
                    RiskScore = farmSnap.RiskScore,
                    IsVerified = farmSnap.IsVerified,
                    DeliveryPoint = requestTerms?.DeliveryPoint.ToString(),
                    FreightPayer = requestTerms?.FreightPayer.ToString(),
                    TransitRisk = requestTerms?.TransitRisk.ToString(),
                    LocationMatched = match.LocationMatched,
                    MatchScore = match.MatchScore
                }.ToJson();
            }

            if (existingByFarmId.TryGetValue(match.FarmId, out var existing))
            {
                // Factory exclusion survives re-runs — never revive.
                if (existing.IsExcludedByFactory)
                {
                    match.MatchId = existing.MatchId;
                    continue;
                }

                existing.MatchScore = match.MatchScore;
                existing.RiskScore = match.RiskScore;
                existing.IsGeographicExpansion = match.IsGeographicExpansion;
                if (string.IsNullOrWhiteSpace(existing.MatchedGovernorate))
                    existing.MatchedGovernorate = govSnapshot;

                if (existing.Status is FarmMatchStatus.Expired or FarmMatchStatus.Rejected)
                {
                    existing.Status = FarmMatchStatus.Proposed;
                    existing.MatchedGovernorate = govSnapshot;
                    existing.EligibilitySnapshotJson = eligibilityJson;
                    newProposedFarmIds.Add(match.FarmId);
                }
                else if (existing.Status == FarmMatchStatus.Proposed
                         && string.IsNullOrWhiteSpace(existing.EligibilitySnapshotJson)
                         && eligibilityJson is not null)
                {
                    existing.EligibilitySnapshotJson = eligibilityJson;
                }

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
                MatchedGovernorate = govSnapshot,
                EligibilitySnapshotJson = eligibilityJson,
                IsGeographicExpansion = match.IsGeographicExpansion,
                Status = FarmMatchStatus.Proposed,
                CreatedAt = DateTime.UtcNow
            });
            match.MatchId = matchId;
            newProposedFarmIds.Add(match.FarmId);
            persistedCount++;
        }

        if (newProposedFarmIds.Count > 0)
        {
            var farmUsers = await _context.Farm
                .AsNoTracking()
                .Where(f => newProposedFarmIds.Contains(f.FarmId))
                .Select(f => new { f.FarmId, f.UserId })
                .ToListAsync();

            foreach (var farm in farmUsers)
            {
                if (farm.UserId == Guid.Empty)
                    continue;

                var matchId = matches.FirstOrDefault(m => m.FarmId == farm.FarmId)?.MatchId;
                _context.Notifications.Add(new Notification
                {
                    NotificationId = Guid.NewGuid(),
                    UserId = farm.UserId,
                    Title = "notifications.types.matchProposed.title",
                    Message = "notifications.types.matchProposed.body",
                    Type = "MatchProposed",
                    RelatedEntityType = NotificationRelations.Match,
                    RelatedEntityId = matchId is Guid mid && mid != Guid.Empty ? mid : null,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
                _context.ChannelMessages.Add(new ChannelMessage
                {
                    ChannelMessageId = Guid.NewGuid(),
                    Channel = "WhatsApp",
                    ToPhone = "unknown",
                    UserId = farm.UserId,
                    TemplateKey = "MatchProposed",
                    Body = "A factory proposed a supply match on NileChain.",
                    Status = ChannelMessageStatus.Logged,
                    RelatedEntityType = NotificationRelations.Match,
                    RelatedEntityId = matchId is Guid mid2 && mid2 != Guid.Empty ? mid2 : null,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        if (persistedCount > 0)
        {
            var supplyRequest = await _context.SupplyRequests
                .FirstOrDefaultAsync(r => r.RequestId == requestId);
            if (supplyRequest is not null
                && supplyRequest.Status == NileChain.Domain.Enums.SupplyRequestStatus.Pending)
            {
                supplyRequest.Status = NileChain.Domain.Enums.SupplyRequestStatus.Matched;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted {Count} FarmMatch rows for RequestId {RequestId} " +
            "(superseded={Superseded}, newlyProposed={New})",
            persistedCount,
            requestId,
            staleMatches.Count,
            newProposedFarmIds.Count);

        return staleMatches.Count;
    }

    private void NotifyMatchSuperseded(Guid? userId, Guid matchId)
    {
        if (userId is not Guid uid || uid == Guid.Empty)
            return;

        _context.Notifications.Add(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = uid,
            Title = "notifications.types.matchSuperseded.title",
            Message = "notifications.types.matchSuperseded.body",
            Type = "MatchSuperseded",
            RelatedEntityType = NotificationRelations.Match,
            RelatedEntityId = matchId == Guid.Empty ? null : matchId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });
    }

    private void DetachAddedFarmMatches()
    {
        foreach (var entry in _context.ChangeTracker.Entries<FarmMatch>()
                     .Where(e => e.State == EntityState.Added)
                     .ToList())
        {
            entry.State = EntityState.Detached;
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
