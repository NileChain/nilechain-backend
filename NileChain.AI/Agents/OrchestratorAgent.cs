using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NileChain.AI.Models;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Agents;

public class OrchestratorAgent
{
    /// <summary>
    /// Must match MatchingPlugin risk contribution: (RiskScore / 100) * 20.
    /// </summary>
    private const decimal RiskMatchContributionMax = 20m;

    private readonly MatchingAgent _matchingAgent;
    private readonly RiskAgent _riskAgent;
    private readonly NileChainDbContext _context;
    private readonly ILogger<OrchestratorAgent> _logger;

    public OrchestratorAgent(
        MatchingAgent matchingAgent,
        RiskAgent riskAgent,
        NileChainDbContext context,
        ILogger<OrchestratorAgent> logger)
    {
        _matchingAgent = matchingAgent;
        _riskAgent = riskAgent;
        _context = context;
        _logger = logger;
    }

    public async Task<AgentResponse> RunAsync(AgentRequest request)
    {
        try
        {
            // 1) Matching once — MatchingAgent → MatchingPlugin
            var matches = await _matchingAgent.RunAsync(request);

            if (matches.Count == 0)
            {
                return new AgentResponse
                {
                    Success = false,
                    ErrorMessage = "No matching farms found"
                };
            }

            // 2) Risk once per matched farm — RiskAgent → RiskPlugin (no re-matching)
            var riskReports = await _riskAgent.RunAsync(matches);

            // 3) Attach successful RiskReports; keep MatchingPlugin scores if risk failed
            foreach (var match in matches)
            {
                var report = riskReports.FirstOrDefault(r => r.FarmId == match.FarmId);
                if (report is null)
                    continue;

                // Always expose the full RiskReport (additive for API consumers).
                match.RiskReport = report;

                if (IsFailedRiskReport(report))
                    continue;

                // MatchScore already includes a risk contribution from MatchingPlugin
                // based on the previous Farm.RiskScore. Replace that portion with the
                // freshly calculated OverallScore so final ranking uses updated risk.
                var previousRiskContribution =
                    (match.RiskScore / 100m) * RiskMatchContributionMax;
                var updatedRiskContribution =
                    (report.OverallScore / 100m) * RiskMatchContributionMax;

                match.MatchScore =
                    match.MatchScore - previousRiskContribution + updatedRiskContribution;
                match.RiskScore = report.OverallScore;
                match.RiskLevel = report.RiskLevel;
            }

            // 4) Final ranking with updated RiskScore values
            matches = matches
                .OrderByDescending(m => m.MatchScore)
                .ThenByDescending(m => m.RiskScore)
                .ThenByDescending(m => m.IsVerified)
                .ToList();

            // 5) Persist FarmMatch rows (soft-fail — never break the AI response)
            await PersistFarmMatchesAsync(request.RequestId, matches);

            return new AgentResponse
            {
                Success = true,
                TopMatches = matches,
            };
        }
        catch (Exception ex)
        {
            return new AgentResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Upserts one FarmMatch per ranked farm for (RequestId, FarmId).
    /// Seeder and domain usage treat that pair as logical unique; DB has no unique index.
    /// New rows use Proposed; existing rows keep Status (Accept/Reject/Contract flow intact).
    /// </summary>
    private async Task PersistFarmMatchesAsync(Guid requestId, List<MatchResult> matches)
    {
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
                    persistedCount++;
                    continue;
                }

                _context.FarmMatches.Add(new FarmMatch
                {
                    MatchId = Guid.NewGuid(),
                    RequestId = requestId,
                    FarmId = match.FarmId,
                    MatchScore = match.MatchScore,
                    RiskScore = match.RiskScore,
                    Status = FarmMatchStatus.Proposed,
                    CreatedAt = DateTime.UtcNow
                });
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

    private static bool IsFailedRiskReport(RiskReport report) =>
        !string.IsNullOrWhiteSpace(report.AIAnalysis)
        && string.IsNullOrWhiteSpace(report.RiskLevel);
}
