using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.Domain.Entities;
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
    public async Task<RiskReport> CalculateRiskScore(
        [Description("The farm ID to evaluate")] string farmId)
    {
        if (!Guid.TryParse(farmId, out var farmGuid))
        {
            return new RiskReport { AIAnalysis = "Invalid farm ID" };
        }

        var farm = await _context.Farm
            .Include(f => f.CropTypes)
            .Include(f => f.FarmCertifications)
            .FirstOrDefaultAsync(f => f.FarmId == farmGuid);

        if (farm is null)
        {
            return new RiskReport { AIAnalysis = "Farm not found" };
        }

        var completedContracts = await _context.Contracts
            .CountAsync(c => c.FarmMatch.FarmId == farm.FarmId
                             && c.Status == ContractStatus.Signed);

        var avgRating = await _context.Reviews
            .Where(r => r.TargetId == farm.UserId)
            .AverageAsync(r => (decimal?)r.Rating) ?? 0;

        var profileScore = CalculateProfileScore(farm);
        var certScore = farm.IsVerified || farm.FarmCertifications.Count > 0 ? 25m : 0m;
        var contractScore = Math.Min(completedContracts * 10, 30);
        var ratingScore = (avgRating / 5m) * 20m;
        var overallScore = profileScore + certScore + contractScore + ratingScore;

        farm.RiskScore = overallScore;
        await _context.SaveChangesAsync();

        return new RiskReport
        {
            FarmId = farm.FarmId,
            FarmName = farm.Name,
            OverallScore = overallScore,
            RiskLevel = GetRiskLevel(overallScore),
            ProfileCompleteness = profileScore,
            CertificationScore = certScore,
            ContractHistoryScore = contractScore,
            RatingScore = ratingScore,
        };
    }

    private static decimal CalculateProfileScore(Farm farm)
    {
        decimal score = 0;
        if (!string.IsNullOrEmpty(farm.Name)) score += 5;
        if (!string.IsNullOrEmpty(farm.Location)) score += 5;
        if (!string.IsNullOrEmpty(farm.Governorate)) score += 5;
        if (farm.SizeInFeddans is > 0) score += 5;
        if (farm.CropTypes.Count > 0) score += 5;
        return score;
    }

    private static string GetRiskLevel(decimal score) => score switch
    {
        >= 70 => "منخفض المخاطر",
        >= 40 => "متوسط المخاطر",
        _ => "عالي المخاطر"
    };
}
