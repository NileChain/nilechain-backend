using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using NileChain.AI.Agents;
using NileChain.AI.Models;
using NileChain.AI.Orchestration;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Plugins;

/// <summary>
/// KernelFunctions exposed to the LLM orchestrator. Wraps MatchingPlugin / RiskPlugin /
/// ContractAgent without changing their core scoring or prompt logic.
/// </summary>
public sealed class OrchestrationToolsPlugin
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly MatchingPlugin _matchingPlugin;
    private readonly RiskPlugin _riskPlugin;
    private readonly Lazy<ContractAgent> _contractAgent;
    private readonly NileChainDbContext _db;
    private readonly ILogger _logger;
    private readonly OrchestrationRunState _state;

    public OrchestrationRunState State => _state;

    public OrchestrationToolsPlugin(
        MatchingPlugin matchingPlugin,
        RiskPlugin riskPlugin,
        Lazy<ContractAgent> contractAgent,
        NileChainDbContext db,
        ILogger logger,
        OrchestrationRunState state)
    {
        _matchingPlugin = matchingPlugin;
        _riskPlugin = riskPlugin;
        _contractAgent = contractAgent;
        _db = db;
        _logger = logger;
        _state = state;
    }

    [KernelFunction("SearchFarms")]
    [Description(
        "Search for candidate farms that grow the requested crop. " +
        "Use FIRST for any new supply request to build a shortlist. " +
        "Pass radiusKm from the current search radius (start at 50). " +
        "If fewer than 3 strong candidates are returned, call WidenSearchRadius then SearchFarms again with the new radius. " +
        "Do NOT call this repeatedly with the same radius.")]
    public async Task<string> SearchFarms(
        [Description("Crop type name, e.g. Wheat, Potato, Corn")] string cropType,
        [Description("Factory / preferred governorate")] string governorate,
        [Description("Quality specifications text")] string qualitySpecs,
        [Description("Search radius in km. Use 50 initially; use the value returned by WidenSearchRadius after widening.")] int radiusKm)
    {
        var args = $"cropType={cropType}; governorate={governorate}; qualitySpecs={Truncate(qualitySpecs, 80)}; radiusKm={radiusKm}";
        try
        {
            _state.CurrentRadiusKm = radiusKm <= 0
                ? OrchestrationRunState.DefaultRadiusKm
                : radiusKm;

            // Core scoring unchanged — MatchingPlugin.FindMatchingFarms(requestId).
            var matches = await _matchingPlugin.FindMatchingFarms(_state.RequestId);

            // Soft geographic preference when radius is still "local".
            // Widened radius (>= 100) keeps the full MatchingPlugin shortlist (all governorates).
            if (_state.CurrentRadiusKm < 100
                && !string.IsNullOrWhiteSpace(governorate))
            {
                var local = matches
                    .Where(m => string.Equals(
                        m.Governorate,
                        governorate,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (local.Count > 0)
                    matches = local;
            }

            _state.LastSearchResults = matches;
            MergeIntoRanked(matches);

            var summary =
                $"found={matches.Count}; radiusKm={_state.CurrentRadiusKm}; " +
                $"farms=[{string.Join(", ", matches.Select(m => $"{m.FarmName}({m.MatchScore:0})"))}]";

            _state.RecordTrail("SearchFarms", args, summary);
            _logger.LogInformation("Tool SearchFarms | {Args} | {Summary}", args, summary);

            return JsonSerializer.Serialize(new
            {
                count = matches.Count,
                radiusKm = _state.CurrentRadiusKm,
                hint = matches.Count < 3
                    ? "Fewer than 3 farms found. Consider WidenSearchRadius once, then SearchFarms again."
                    : "Enough candidates. Next: CalculateRiskScore for top farms.",
                farms = matches.Select(m => new
                {
                    m.FarmId,
                    m.FarmName,
                    m.Governorate,
                    m.MatchScore,
                    m.RiskScore,
                    m.RiskLevel,
                    m.IsVerified
                })
            }, JsonOptions);
        }
        catch (Exception ex)
        {
            _state.RecordTrail("SearchFarms", args, $"ERROR: {ex.Message}");
            _logger.LogError(ex, "SearchFarms failed");
            return JsonSerializer.Serialize(new { error = ex.Message, count = 0 });
        }
    }

    [KernelFunction("CalculateRiskScore")]
    [Description(
        "Calculate a 0-100 supplier risk score and factor breakdown for ONE farm. " +
        "Call this for each shortlisted farm after SearchFarms (or at least the top 1-3). " +
        "If OverallScore is below 40, you MUST call FlagLowRiskWarning before any contract step.")]
    public async Task<string> CalculateRiskScore(
        [Description("Farm ID to evaluate")] Guid farmId)
    {
        var args = $"farmId={farmId}";
        try
        {
            var report = await _riskPlugin.CalculateRiskScore(farmId);

            var match = _state.RankedCandidates.FirstOrDefault(m => m.FarmId == farmId)
                        ?? _state.LastSearchResults.FirstOrDefault(m => m.FarmId == farmId);

            if (match is not null)
            {
                match.RiskReport = report;
                if (!string.IsNullOrWhiteSpace(report.RiskLevel))
                {
                    match.RiskScore = report.OverallScore;
                    match.RiskLevel = report.RiskLevel;
                }

                ReRank();
            }

            var summary =
                $"farm={report.FarmName}; score={report.OverallScore:0}; level={report.RiskLevel}";
            _state.RecordTrail("CalculateRiskScore", args, summary);
            _logger.LogInformation("Tool CalculateRiskScore | {Args} | {Summary}", args, summary);

            return JsonSerializer.Serialize(report, JsonOptions);
        }
        catch (Exception ex)
        {
            _state.RecordTrail("CalculateRiskScore", args, $"ERROR: {ex.Message}");
            _logger.LogError(ex, "CalculateRiskScore failed for {FarmId}", farmId);
            return JsonSerializer.Serialize(new { error = ex.Message, farmId });
        }
    }

    [KernelFunction("WidenSearchRadius")]
    [Description(
        "Increase the geographic search radius when SearchFarms returned too few farms. " +
        "HARD LIMIT: call at most ONCE per request. Returns the new radiusKm to pass into SearchFarms. " +
        "Do not call if you already widened once.")]
    public string WidenSearchRadius(
        [Description("Current radius in km")] int currentRadiusKm)
    {
        var args = $"currentRadiusKm={currentRadiusKm}";

        if (_state.WidenCallCount >= OrchestrationRunState.MaxWidenCalls)
        {
            _state.PartialResult = true;
            _state.PartialReason =
                "WidenSearchRadius hard cap reached (max 1). Returning best available results.";
            var blocked =
                $"BLOCKED: max {OrchestrationRunState.MaxWidenCalls} widen already used. " +
                $"Keep radius={_state.CurrentRadiusKm}. PartialResult=true.";
            _state.RecordTrail("WidenSearchRadius", args, blocked, blocked: true, blockReason: blocked);
            _logger.LogWarning("Tool WidenSearchRadius BLOCKED | {Args}", args);
            return JsonSerializer.Serialize(new
            {
                blocked = true,
                reason = blocked,
                radiusKm = _state.CurrentRadiusKm,
                partialResult = true
            });
        }

        var baseRadius = currentRadiusKm > 0 ? currentRadiusKm : _state.CurrentRadiusKm;
        var newRadius = Math.Min(
            baseRadius + OrchestrationRunState.RadiusStepKm,
            OrchestrationRunState.MaxRadiusKm);

        _state.WidenCallCount++;
        _state.CurrentRadiusKm = newRadius;

        var summary = $"widenCount={_state.WidenCallCount}; newRadiusKm={newRadius}";
        _state.RecordTrail("WidenSearchRadius", args, summary);
        _logger.LogInformation("Tool WidenSearchRadius | {Args} | {Summary}", args, summary);

        return JsonSerializer.Serialize(new
        {
            radiusKm = newRadius,
            widenCallCount = _state.WidenCallCount,
            maxWidens = OrchestrationRunState.MaxWidenCalls,
            nextStep = "Call SearchFarms again with this radiusKm."
        });
    }

    [KernelFunction("ProposeNextBestMatch")]
    [Description(
        "Propose the next-best ranked farm after the factory rejects a candidate. " +
        "HARD LIMIT: call at most ONCE per request. Excludes the rejected farm from future proposals.")]
    public string ProposeNextBestMatch(
        [Description("Farm ID that was rejected")] Guid rejectedFarmId,
        [Description("Supply request ID")] Guid requestId)
    {
        var args = $"rejectedFarmId={rejectedFarmId}; requestId={requestId}";

        if (_state.ProposeNextCallCount >= OrchestrationRunState.MaxProposeNextCalls)
        {
            _state.PartialResult = true;
            _state.PartialReason =
                "ProposeNextBestMatch hard cap reached (max 1). Returning best available results.";
            var blocked =
                $"BLOCKED: max {OrchestrationRunState.MaxProposeNextCalls} propose-next already used. PartialResult=true.";
            _state.RecordTrail("ProposeNextBestMatch", args, blocked, blocked: true, blockReason: blocked);
            _logger.LogWarning("Tool ProposeNextBestMatch BLOCKED | {Args}", args);
            return JsonSerializer.Serialize(new
            {
                blocked = true,
                reason = blocked,
                partialResult = true
            });
        }

        _state.RejectedFarmIds.Add(rejectedFarmId);
        _state.ProposeNextCallCount++;

        var next = _state.RankedCandidates
            .Concat(_state.LastSearchResults)
            .GroupBy(m => m.FarmId)
            .Select(g => g.First())
            .Where(m => !_state.RejectedFarmIds.Contains(m.FarmId))
            .OrderByDescending(m => m.MatchScore)
            .ThenByDescending(m => m.RiskScore)
            .ThenByDescending(m => m.IsVerified)
            .FirstOrDefault();

        if (next is null)
        {
            _state.PartialResult = true;
            _state.PartialReason = "No alternate farm available after rejection.";
            var summary = "no alternate farm; PartialResult=true";
            _state.RecordTrail("ProposeNextBestMatch", args, summary);
            return JsonSerializer.Serialize(new
            {
                found = false,
                proposeCallCount = _state.ProposeNextCallCount,
                partialResult = true,
                message = summary
            });
        }

        var ok =
            $"next={next.FarmName} ({next.FarmId}); score={next.MatchScore:0}; risk={next.RiskScore:0}";
        _state.RecordTrail("ProposeNextBestMatch", args, ok);
        _logger.LogInformation("Tool ProposeNextBestMatch | {Args} | {Summary}", args, ok);

        return JsonSerializer.Serialize(new
        {
            found = true,
            proposeCallCount = _state.ProposeNextCallCount,
            farm = new
            {
                next.FarmId,
                next.FarmName,
                next.Governorate,
                next.MatchScore,
                next.RiskScore,
                next.RiskLevel,
                next.IsVerified
            }
        });
    }

    [KernelFunction("FlagLowRiskWarning")]
    [Description(
        "Flag a high-risk (low score) farm before contracting. " +
        "MUST be called when CalculateRiskScore returns OverallScore < 40. " +
        "After this fires, GenerateContract is BLOCKED until the factory sets ConfirmHighRiskWarning=true on the request. " +
        "Surface the warning to the caller; do not silently proceed to GenerateContract.")]
    public string FlagLowRiskWarning(
        [Description("Farm ID being flagged")] Guid farmId,
        [Description("Risk score 0-100 from CalculateRiskScore")] int riskScore)
    {
        var args = $"farmId={farmId}; riskScore={riskScore}";

        if (riskScore >= OrchestrationRunState.LowRiskThreshold)
        {
            var msg =
                $"No warning needed: riskScore {riskScore} is >= {OrchestrationRunState.LowRiskThreshold}.";
            _state.RecordTrail("FlagLowRiskWarning", args, msg);
            return JsonSerializer.Serialize(new { warned = false, message = msg });
        }

        _state.RiskWarningActive = true;
        _state.WarnedFarmId = farmId;
        _state.WarnedRiskScore = riskScore;

        var warning = new RiskWarningResult
        {
            FarmId = farmId,
            RiskScore = riskScore,
            RequiresFactoryConfirmation = true,
            Message =
                $"HIGH RISK WARNING: farm {farmId} scored {riskScore}/100 " +
                $"(threshold {OrchestrationRunState.LowRiskThreshold}). " +
                "GenerateContract is blocked until ConfirmHighRiskWarning=true."
        };

        _state.RecordTrail("FlagLowRiskWarning", args, warning.Message);
        _logger.LogWarning("Tool FlagLowRiskWarning | {Args} | {Message}", args, warning.Message);

        return JsonSerializer.Serialize(warning, JsonOptions);
    }

    [KernelFunction("GenerateContract")]
    [Description(
        "Generate a legal Arabic supply contract for a confirmed FarmMatch. " +
        "ONLY call AFTER farms were searched, risk assessed, and the factory selected a match. " +
        "NEVER call immediately after FlagLowRiskWarning unless ConfirmHighRiskWarning is true. " +
        "Requires a persisted or known matchId.")]
    public async Task<string> GenerateContract(
        [Description("FarmMatch MatchId to generate a contract for")] Guid matchId)
    {
        var args = $"matchId={matchId}";

        if (_state.RiskWarningActive && !_state.FactoryConfirmedHighRisk)
        {
            var blocked =
                "BLOCKED: FlagLowRiskWarning is active and factory has not confirmed " +
                "(ConfirmHighRiskWarning=false). Do not generate a contract yet.";
            _state.RecordTrail("GenerateContract", args, blocked, blocked: true, blockReason: blocked);
            _logger.LogWarning("Tool GenerateContract BLOCKED | {Args} | {Reason}", args, blocked);
            return JsonSerializer.Serialize(new
            {
                blocked = true,
                reason = blocked,
                requiresConfirmation = true
            });
        }

        try
        {
            var match = await _db.FarmMatches
                .AsNoTracking()
                .Include(m => m.Farm)
                .Include(m => m.SupplyRequest)
                    .ThenInclude(r => r!.Factory)
                .Include(m => m.SupplyRequest)
                    .ThenInclude(r => r!.CropType)
                .FirstOrDefaultAsync(m => m.MatchId == matchId);

            MatchResult selectedFarm;
            AgentRequest agentRequest;
            string factoryName;

            if (match is not null)
            {
                selectedFarm = new MatchResult
                {
                    FarmId = match.FarmId,
                    MatchId = match.MatchId,
                    FarmName = match.Farm?.Name ?? "Farm",
                    Governorate = match.Farm?.Governorate ?? string.Empty,
                    MatchScore = match.MatchScore ?? 0m,
                    RiskScore = match.RiskScore ?? 0m,
                    IsVerified = match.Farm?.IsVerified ?? false
                };

                agentRequest = new AgentRequest
                {
                    RequestId = match.RequestId,
                    CropType = match.SupplyRequest?.CropType?.Name ?? _state.Request.CropType,
                    QuantityTons = match.SupplyRequest?.QuantityTons ?? _state.Request.QuantityTons,
                    QualitySpecs = match.SupplyRequest?.QualitySpecs ?? _state.Request.QualitySpecs,
                    PricePerTon = match.SupplyRequest?.PricePerTon ?? _state.Request.PricePerTon,
                    DeliveryDate = match.SupplyRequest?.DeliveryDate ?? _state.Request.DeliveryDate,
                    FactoryGovernorate = match.SupplyRequest?.Factory?.Governorate
                        ?? _state.Request.FactoryGovernorate
                };

                factoryName = match.SupplyRequest?.Factory?.Name
                    ?? _state.FactoryName
                    ?? "Factory";
            }
            else
            {
                // LLM often passes farmId instead of FarmMatch.MatchId (matches not persisted yet).
                // Prefer the farm the model named, not merely the top-ranked shortlist entry.
                var byFarmId = ResolveCandidateByFarmId(matchId);
                if (byFarmId is null)
                {
                    var farmEntity = await _db.Farm
                        .AsNoTracking()
                        .FirstOrDefaultAsync(f => f.FarmId == matchId);
                    if (farmEntity is not null)
                    {
                        byFarmId = new MatchResult
                        {
                            FarmId = farmEntity.FarmId,
                            FarmName = farmEntity.Name,
                            Governorate = farmEntity.Governorate ?? string.Empty,
                            RiskScore = farmEntity.RiskScore ?? 0m,
                            IsVerified = farmEntity.IsVerified
                        };
                    }
                }

                if (byFarmId is null)
                {
                    var noFarm =
                        "No FarmMatch or farm found for the given id. " +
                        "Pass FarmMatch.MatchId, or a FarmId from SearchFarms results.";
                    _state.RecordTrail("GenerateContract", args, noFarm, blocked: true, blockReason: noFarm);
                    return JsonSerializer.Serialize(new { blocked = true, reason = noFarm });
                }

                selectedFarm = byFarmId;
                agentRequest = _state.Request;
                factoryName = _state.FactoryName ?? "Factory";
                _logger.LogInformation(
                    "GenerateContract resolved id {Id} as FarmId for farm {FarmName}",
                    matchId,
                    selectedFarm.FarmName);
            }

            string contractText;
            var usedTemplateFallback = false;

            try
            {
                var result = await _contractAgent.Value.GenerateContractAsync(
                    agentRequest,
                    selectedFarm,
                    factoryName);

                if (!result.Success || string.IsNullOrWhiteSpace(result.ContractText))
                {
                    contractText = BuildTemplateContract(selectedFarm.FarmName, factoryName, agentRequest);
                    usedTemplateFallback = true;
                }
                else
                {
                    contractText = result.ContractText;
                }
            }
            catch (Exception ex)
            {
                // RAG/Chroma/OpenAI failure → template-only path (do not fail the whole request).
                _logger.LogWarning(ex,
                    "GenerateContract LLM/RAG failed; using template fallback for match {MatchId}",
                    matchId);
                contractText = BuildTemplateContract(selectedFarm.FarmName, factoryName, agentRequest);
                usedTemplateFallback = true;
            }

            // Post-generation: farm name in draft must match the selected farm record.
            if (!ContractMentionsFarmName(contractText, selectedFarm.FarmName))
            {
                _logger.LogWarning(
                    "GenerateContract farm-name mismatch: expected '{ExpectedFarm}' in draft for id {Id}; regenerating with template",
                    selectedFarm.FarmName,
                    matchId);
                contractText = BuildTemplateContract(selectedFarm.FarmName, factoryName, agentRequest);
                usedTemplateFallback = true;
                _state.RecordTrail(
                    "GenerateContract",
                    args,
                    $"farm-name mismatch corrected via template; expected={selectedFarm.FarmName}");
            }

            if (!ValidateContractFields(contractText, agentRequest, selectedFarm.FarmName, factoryName,
                    out var validationError))
            {
                _logger.LogWarning(
                    "GenerateContract incomplete LLM draft ({Error}); regenerating with template for farm {Farm}",
                    validationError,
                    selectedFarm.FarmName);
                contractText = BuildTemplateContract(selectedFarm.FarmName, factoryName, agentRequest);
                usedTemplateFallback = true;
                _state.RecordTrail(
                    "GenerateContract",
                    args,
                    $"incomplete LLM draft corrected via template: {validationError}");

                if (!ValidateContractFields(contractText, agentRequest, selectedFarm.FarmName, factoryName,
                        out validationError))
                {
                    _state.ContractIncomplete = true;
                    _state.ContractValidationError = validationError;
                    _state.ContractDraft = contractText;
                    var incomplete = $"INCOMPLETE DRAFT rejected: {validationError}";
                    _state.RecordTrail("GenerateContract", args, incomplete, blocked: true, blockReason: incomplete);
                    return JsonSerializer.Serialize(new
                    {
                        success = false,
                        incomplete = true,
                        validationError,
                        usedTemplateFallback,
                        farmName = selectedFarm.FarmName
                    });
                }
            }

            _state.ContractDraft = contractText;
            _state.ContractIncomplete = false;
            _state.ContractValidationError = null;

            // Persist Contract entity when we have a known matchId.
            if (match is not null)
            {
                var existingContract = await _db.Contracts
                    .FirstOrDefaultAsync(c => c.MatchId == match.MatchId);
                if (existingContract is null)
                {
                    _db.Contracts.Add(new NileChain.Domain.Entities.Contract
                    {
                        ContractId = Guid.NewGuid(),
                        MatchId = match.MatchId,
                        GeneratedText = contractText,
                        Status = NileChain.Domain.Enums.ContractStatus.PendingSignature,
                        CreatedAt = DateTime.UtcNow
                    });
                }
                else
                {
                    existingContract.GeneratedText = contractText;
                    if (existingContract.Status == NileChain.Domain.Enums.ContractStatus.Cancelled)
                        existingContract.Status = NileChain.Domain.Enums.ContractStatus.PendingSignature;
                }

                await _db.SaveChangesAsync();
            }

            var summary =
                $"ok; farm={selectedFarm.FarmName}; chars={contractText.Length}; templateFallback={usedTemplateFallback}";
            _state.RecordTrail("GenerateContract", args, summary);
            _logger.LogInformation("Tool GenerateContract | {Args} | {Summary}", args, summary);

            return JsonSerializer.Serialize(new
            {
                success = true,
                usedTemplateFallback,
                farmName = selectedFarm.FarmName,
                farmId = selectedFarm.FarmId,
                contractPreview = Truncate(contractText, 400),
                characterCount = contractText.Length,
                matchId
            });
        }
        catch (Exception ex)
        {
            _state.RecordTrail("GenerateContract", args, $"ERROR: {ex.Message}");
            _logger.LogError(ex, "GenerateContract failed for {MatchId}", matchId);
            return JsonSerializer.Serialize(new { success = false, error = ex.Message });
        }
    }

    private MatchResult? ResolveCandidateByFarmId(Guid farmId) =>
        _state.RankedCandidates.FirstOrDefault(m => m.FarmId == farmId)
        ?? _state.LastSearchResults.FirstOrDefault(m => m.FarmId == farmId);

    private static bool ContractMentionsFarmName(string contractText, string farmName)
    {
        if (string.IsNullOrWhiteSpace(farmName))
            return false;
        if (contractText.Contains(farmName, StringComparison.OrdinalIgnoreCase))
            return true;

        // LLM sometimes drops the " (Demo)" / " — Owner" suffix; accept the core name.
        var core = farmName;
        var paren = core.IndexOf('(', StringComparison.Ordinal);
        if (paren > 0)
            core = core[..paren].Trim();
        var dash = core.IndexOf('—', StringComparison.Ordinal);
        if (dash < 0)
            dash = core.IndexOf('-', StringComparison.Ordinal);
        if (dash > 0)
            core = core[..dash].Trim();

        return !string.IsNullOrWhiteSpace(core)
               && contractText.Contains(core, StringComparison.OrdinalIgnoreCase);
    }

    private void MergeIntoRanked(List<MatchResult> matches)
    {
        var map = _state.RankedCandidates.ToDictionary(m => m.FarmId);
        foreach (var m in matches)
            map[m.FarmId] = m;

        _state.RankedCandidates = map.Values
            .OrderByDescending(m => m.MatchScore)
            .ThenByDescending(m => m.RiskScore)
            .ThenByDescending(m => m.IsVerified)
            .ToList();
    }

    private void ReRank()
    {
        _state.RankedCandidates = _state.RankedCandidates
            .OrderByDescending(m => m.MatchScore)
            .ThenByDescending(m => m.RiskScore)
            .ThenByDescending(m => m.IsVerified)
            .ToList();
    }

    internal static bool ValidateContractFields(
        string contractText,
        AgentRequest request,
        string farmName,
        string factoryName,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(contractText))
        {
            error = "Contract text is empty.";
            return false;
        }

        var missing = new List<string>();
        // Farm / factory names must appear as exact strings (not just the generic party labels).
        if (!ContainsAny(contractText, farmName))
            missing.Add("parties/farm");
        if (!ContainsAny(contractText, factoryName))
            missing.Add("parties/factory");
        if (!ContainsAny(contractText, request.CropType, "المحصول"))
            missing.Add("crop");
        if (!ContainsAny(contractText, request.QuantityTons.ToString("0"), "الكمية", "طن"))
            missing.Add("quantity");
        if (!ContainsAny(contractText, request.PricePerTon.ToString("0"), "السعر", "جنيه"))
            missing.Add("price");
        if (!ContainsAny(contractText,
                request.DeliveryDate.ToString("yyyy"),
                request.DeliveryDate.ToString("dd"),
                "التسليم",
                "التوريد"))
            missing.Add("delivery date");

        if (missing.Count == 0)
            return true;

        error = "Missing required fields: " + string.Join(", ", missing);
        return false;
    }

    private static bool ContainsAny(string haystack, params string?[] needles)
    {
        foreach (var n in needles)
        {
            if (string.IsNullOrWhiteSpace(n))
                continue;
            if (haystack.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string BuildTemplateContract(string farmName, string factoryName, AgentRequest request)
    {
        var total = request.QuantityTons * request.PricePerTon;
        var sb = new StringBuilder();
        sb.AppendLine("بسم الله الرحمن الرحيم");
        sb.AppendLine();
        sb.AppendLine("عقد توريد زراعي (نموذج احتياطي — تم إنشاؤه بدون RAG/LLM)");
        sb.AppendLine();
        sb.AppendLine($"الطرف الأول (المورد): {farmName}");
        sb.AppendLine($"الطرف الثاني (المشتري): {factoryName}");
        sb.AppendLine($"المحصول: {request.CropType}");
        sb.AppendLine($"الكمية: {request.QuantityTons:0.##} طن متري");
        sb.AppendLine($"السعر: {request.PricePerTon:0.##} جنيه/طن");
        sb.AppendLine($"الإجمالي: {total:0.##} جنيه مصري");
        sb.AppendLine($"تاريخ التسليم: {request.DeliveryDate:dd MMMM yyyy}");
        sb.AppendLine($"مواصفات الجودة: {request.QualitySpecs}");
        sb.AppendLine();
        sb.AppendLine("شروط الدفع: 30% مقدم، 70% عند الاستلام.");
        sb.AppendLine("فض النزاعات: محاكم القاهرة الاقتصادية.");
        sb.AppendLine();
        sb.AppendLine("توقيع المورد: __________");
        sb.AppendLine("توقيع المشتري: __________");
        return sb.ToString();
    }

    private static string Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        return value.Length <= max ? value : value[..max] + "…";
    }
}
