using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using NileChain.AI.Models;
using NileChain.Domain.Entities;
using NileChain.Infrastructure.Persistence;

namespace NileChain.AI.Plugins;

public class MatchingPlugin
{
    private readonly NileChainDbContext _context;

    public MatchingPlugin(NileChainDbContext context)
    {
        _context = context;
    }

    [KernelFunction("find_matching_farms")]
    [Description("Searches the database for farms that match the supply request criteria")]
    public async Task<List<MatchResult>> FindMatchingFarms(
        [Description("The crop type requested")] string cropType,
        [Description("The quantity in tons")] decimal quantityTons,
        [Description("The factory governorate")] string governorate)
    {
        _ = quantityTons;

        var farms = await _context.Farm
            .AsNoTracking()
            .Include(f => f.CropTypes)
            .Where(f => f.CropTypes.Any(c => c.Name == cropType))
            .ToListAsync();

        var results = farms.Select(farm => new MatchResult
            {
                FarmId = farm.FarmId,
                FarmName = farm.Name,
                Governorate = farm.Governorate ?? string.Empty,
                MatchScore = CalculateMatchScore(farm, cropType, governorate),
                RiskScore = farm.RiskScore ?? 0,
                RiskLevel = GetRiskLevel(farm.RiskScore ?? 0),
                IsVerified = farm.IsVerified,
                CropTypes = farm.CropTypes.Select(c => c.Name).ToList(),
            })
            .OrderByDescending(r => r.MatchScore)
            .Take(5)
            .ToList();

        return results;
    }

    private static decimal CalculateMatchScore(Farm farm, string cropType, string governorate)
    {
        decimal score = 0;
        if (farm.CropTypes.Any(c => c.Name == cropType)) score += 40;
        if (string.Equals(farm.Governorate, governorate, StringComparison.OrdinalIgnoreCase)) score += 20;
        if (farm.IsVerified) score += 20;
        if ((farm.RiskScore ?? 0) >= 70) score += 20;
        return score;
    }

    private static string GetRiskLevel(decimal score) => score switch
    {
        >= 70 => "منخفض المخاطر",
        >= 40 => "متوسط المخاطر",
        _ => "عالي المخاطر"
    };
}
