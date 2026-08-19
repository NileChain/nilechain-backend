using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.Domain.Common;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Plugins;

public class RiskPlugin
{
    private readonly NileChainDbContext _context;

    public RiskPlugin(NileChainDbContext context)
    {
        _context = context;
    }

    [KernelFunction("calculate_risk_score")]
    [Description("Calculates a detailed risk score for a specific farm")]
    public Task<RiskReport> CalculateRiskScore(
        [Description("The farm ID to evaluate")] Guid farmId) =>
        CalculateAsync(farmId, persist: true);

    /// <summary>
    /// Computes the deterministic trust score. When <paramref name="persist"/> is true the
    /// cached <c>Farm.RiskScore</c> column is refreshed, because matching ranks on that column.
    /// </summary>
    public async Task<RiskReport> CalculateAsync(Guid farmId, bool persist)
    {
        var farm = await _context.Farm
            .Include(f => f.User)
            .Include(f => f.FarmCrops)
            .Include(f => f.FarmDocuments)
            .Include(f => f.FarmImages)
            .Include(f => f.FarmCertifications)
            .FirstOrDefaultAsync(f => f.FarmId == farmId);

        if (farm is null)
        {
            return new RiskReport
            {
                FarmId = farmId,
                AIAnalysis = "Farm not found"
            };
        }

        // Completed = Signed. No separate "Fulfilled" contract status exists.
        var signedContracts = await _context.Contracts
            .AsNoTracking()
            .CountAsync(c =>
                c.FarmMatch.FarmId == farmId
                && c.Status == ContractStatus.Signed);

        var averageRating = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.TargetId == farm.UserId)
            .AverageAsync(r => (decimal?)r.Rating) ?? 0m;

        var breakdown = FarmTrustScore.Compute(
            FarmTrustScore.InputsFrom(farm, signedContracts, averageRating, DateTime.UtcNow));

        if (persist && farm.RiskScore != breakdown.Overall)
        {
            farm.RiskScore = breakdown.Overall;
            await _context.SaveChangesAsync();
        }

        return new RiskReport
        {
            FarmId = farm.FarmId,
            FarmName = farm.Name,
            OverallScore = breakdown.Overall,
            RiskLevel = FarmTrustLevelText.Arabic(breakdown.Band),
            ProfileCompleteness = breakdown.Profile,
            CertificationScore = breakdown.Certifications,
            ContractHistoryScore = breakdown.ContractHistory,
            RatingScore = breakdown.Rating
        };
    }
}
