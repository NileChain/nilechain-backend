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
    private const decimal ProfileMaxPoints = 25m;
    private const decimal CertificationMaxPoints = 25m;
    private const decimal ContractMaxPoints = 30m;
    private const decimal RatingMaxPoints = 20m;
    private const decimal MaxOverallScore = 100m;

    // Profile field weights (sum = 25). Only fields that exist on Farm / ApplicationUser / FarmDocument.
    private const decimal ProfileNamePoints = 4m;
    private const decimal ProfileLocationPoints = 4m;
    private const decimal ProfileGovernoratePoints = 4m;
    private const decimal ProfilePhonePoints = 3m;
    private const decimal ProfileSizePoints = 4m;
    private const decimal ProfileDocumentsPoints = 3m;
    private const decimal ProfileCropsPoints = 3m;

    private const decimal PointsPerCertification = 12.5m;
    private const decimal PointsPerSignedContract = 10m;
    private const int MaxRatingValue = 5;

    private readonly NileChainDbContext _context;

    public RiskPlugin(NileChainDbContext context)
    {
        _context = context;
    }

    [KernelFunction("calculate_risk_score")]
    [Description("Calculates a detailed risk score for a specific farm")]
    public async Task<RiskReport> CalculateRiskScore(
        [Description("The farm ID to evaluate")] Guid farmId)
    {
        var farm = await _context.Farm
            .Include(f => f.User)
            .Include(f => f.CropTypes)
            .Include(f => f.FarmDocuments)
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

        var profileScore = CalculateProfileScore(farm);
        var certScore = CalculateCertificationScore(farm);
        var contractScore = await CalculateContractScoreAsync(farm.FarmId);
        var ratingScore = await CalculateRatingScoreAsync(farm.UserId);

        var overallScore = Clamp(
            profileScore + certScore + contractScore + ratingScore,
            0m,
            MaxOverallScore);

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
            RatingScore = ratingScore
        };
    }

    private static decimal CalculateProfileScore(Farm farm)
    {
        // TODO: Farm has no Description field — cannot score description.
        // TODO: Farm has no dedicated Images collection — only FarmDocument exists (scored below as documents).

        decimal score = 0;

        if (!string.IsNullOrWhiteSpace(farm.Name))
            score += ProfileNamePoints;

        if (!string.IsNullOrWhiteSpace(farm.Location))
            score += ProfileLocationPoints;

        if (!string.IsNullOrWhiteSpace(farm.Governorate))
            score += ProfileGovernoratePoints;

        if (!string.IsNullOrWhiteSpace(farm.User?.PhoneNumber))
            score += ProfilePhonePoints;

        if (farm.SizeInFeddans is > 0)
            score += ProfileSizePoints;

        if (farm.FarmDocuments is { Count: > 0 })
            score += ProfileDocumentsPoints;

        if (farm.CropTypes is { Count: > 0 })
            score += ProfileCropsPoints;

        return Clamp(score, 0m, ProfileMaxPoints);
    }

    private static decimal CalculateCertificationScore(Farm farm)
    {
        var certifications = farm.FarmCertifications;
        if (certifications is null || certifications.Count == 0)
            return 0m;

        var now = DateTime.UtcNow;
        var validCount = certifications.Count(c =>
            c.ExpiresAt is null || c.ExpiresAt > now);

        // TODO: No admin API to assign FarmCertification / Certification catalog yet — score is 0 until data exists.
        return Clamp(validCount * PointsPerCertification, 0m, CertificationMaxPoints);
    }

    private async Task<decimal> CalculateContractScoreAsync(Guid farmId)
    {
        // Completed = Signed. No separate "Fulfilled" contract status exists.
        var signedCount = await _context.Contracts
            .AsNoTracking()
            .CountAsync(c =>
                c.FarmMatch.FarmId == farmId
                && c.Status == ContractStatus.Signed);

        // FarmMatch rows are persisted by OrchestratorAgent after each run.
        return Clamp(signedCount * PointsPerSignedContract, 0m, ContractMaxPoints);
    }

    private async Task<decimal> CalculateRatingScoreAsync(Guid farmUserId)
    {
        var avgRating = await _context.Reviews
            .AsNoTracking()
            .Where(r => r.TargetId == farmUserId)
            .AverageAsync(r => (decimal?)r.Rating) ?? 0m;

        // Rating is constrained 1–5 in the database.
        // TODO: No review-creation API yet — score stays 0 until reviews exist.
        var score = (avgRating / MaxRatingValue) * RatingMaxPoints;
        return Clamp(score, 0m, RatingMaxPoints);
    }

    private static decimal Clamp(decimal value, decimal min, decimal max) =>
        Math.Min(Math.Max(value, min), max);

    private static string GetRiskLevel(decimal score) => score switch
    {
        >= 70 => "منخفض المخاطر",
        >= 40 => "متوسط المخاطر",
        _ => "عالي المخاطر"
    };
}
