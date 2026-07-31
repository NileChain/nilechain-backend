using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Plugins;

public class MatchingPlugin
{
    private const decimal CropMatchPoints = 40m;
    private const decimal LocationMatchPoints = 20m;
    private const decimal VerifiedFarmPoints = 20m;
    private const decimal RiskScoreMaxPoints = 20m;

    private readonly NileChainDbContext _context;

    public MatchingPlugin(NileChainDbContext context)
    {
        _context = context;
    }

    [KernelFunction("find_matching_farms")]
    [Description("Searches the database for farms that match the supply request criteria")]
    public async Task<List<MatchResult>> FindMatchingFarms(
        [Description("The supply request ID")] Guid requestId)
    {
        var supplyRequest = await _context.SupplyRequests
            .AsNoTracking()
            .Include(r => r.CropType)
            .Include(r => r.Factory)
            .FirstOrDefaultAsync(r => r.RequestId == requestId);

        if (supplyRequest is null)
            throw new InvalidOperationException($"Supply request '{requestId}' was not found.");

        if (supplyRequest.CropType is null)
            throw new InvalidOperationException(
                $"Crop type for supply request '{requestId}' was not found.");

        var cropTypeId = supplyRequest.CropTypeId;
        var cropTypeName = supplyRequest.CropType.Name;
        var requestedGovernorate = supplyRequest.Factory?.Governorate;

        // TODO: Farm has no capacity/available-quantity field — QuantityTons ({supplyRequest.QuantityTons}) cannot be used for filtering yet.
        // TODO: Farm has no IsActive/IsDeleted flag — inactive farms cannot be excluded yet.

        var candidates = await _context.Farm
            .AsNoTracking()
            .Where(f => f.CropTypes.Any(c => c.CropTypeId == cropTypeId))
            .Select(f => new
            {
                f.FarmId,
                f.Name,
                f.Governorate,
                f.RiskScore,
                f.IsVerified,
                CropTypeNames = f.CropTypes.Select(c => c.Name).ToList()
            })
            .ToListAsync();

        var results = candidates
            .Select(farm =>
            {
                var riskScore = farm.RiskScore ?? 0m;
                var matchScore = CalculateMatchScore(
                    farm.Governorate,
                    requestedGovernorate,
                    farm.IsVerified,
                    riskScore);

                return new MatchResult
                {
                    FarmId = farm.FarmId,
                    FarmName = farm.Name,
                    Governorate = farm.Governorate ?? string.Empty,
                    MatchScore = matchScore,
                    RiskScore = riskScore,
                    RiskLevel = GetRiskLevel(riskScore),
                    IsVerified = farm.IsVerified,
                    CropTypes = farm.CropTypeNames.Count > 0
                        ? farm.CropTypeNames
                        : new List<string> { cropTypeName }
                };
            })
            .OrderByDescending(r => r.MatchScore)
            .ThenByDescending(r => r.RiskScore)
            .ThenByDescending(r => r.IsVerified)
            .Take(5)
            .ToList();

        return results;
    }

    private static decimal CalculateMatchScore(
        string? farmGovernorate,
        string? requestedGovernorate,
        bool isVerified,
        decimal riskScore)
    {
        // Crop match is guaranteed by the candidate query.
        decimal score = CropMatchPoints;

        if (!string.IsNullOrWhiteSpace(requestedGovernorate)
            && string.Equals(farmGovernorate, requestedGovernorate, StringComparison.OrdinalIgnoreCase))
        {
            score += LocationMatchPoints;
        }

        if (isVerified)
            score += VerifiedFarmPoints;

        // Proportional: RiskScore 75 → 15 points (out of 20). Assumes RiskScore is on a 0–100 scale.
        score += (riskScore / 100m) * RiskScoreMaxPoints;

        return score;
    }

    private static string GetRiskLevel(decimal score) => score switch
    {
        >= 70 => "منخفض المخاطر",
        >= 40 => "متوسط المخاطر",
        _ => "عالي المخاطر"
    };
}
