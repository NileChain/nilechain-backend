using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NileChain.AI.Plugins;
using NileChain.Application.Dtos.Farm;
using NileChain.Infrastructure.Persistence;

namespace NileChain.API.Controllers;

/// <summary>
/// Cross-role farm lookups used by factory matching / risk UI.
/// </summary>
[ApiController]
[Route("api/farms")]
[Authorize(Roles = "Factory,Admin,Farm")]
public class FarmsController : ControllerBase
{
    private readonly RiskPlugin _riskPlugin;
    private readonly NileChainDbContext _db;

    public FarmsController(RiskPlugin riskPlugin, NileChainDbContext db)
    {
        _riskPlugin = riskPlugin;
        _db = db;
    }

    /// <summary>
    /// Full risk factor breakdown for a farm (Profile, Certs, Contracts, Ratings).
    /// </summary>
    [HttpGet("{farmId:guid}/risk-report")]
    public async Task<IActionResult> GetRiskReport(Guid farmId)
    {
        var report = await _riskPlugin.CalculateRiskScore(farmId);
        if (string.Equals(report.AIAnalysis, "Farm not found", StringComparison.OrdinalIgnoreCase))
            return NotFound(new { message = "Farm not found", farmId });

        return Ok(report);
    }

    /// <summary>
    /// Read-only public farm profile for factory decision support (no PII beyond display name).
    /// </summary>
    [HttpGet("{farmId:guid}/public-profile")]
    [Authorize(Roles = "Factory,Admin")]
    public async Task<IActionResult> GetPublicProfile(Guid farmId, CancellationToken ct)
    {
        var farm = await _db.Farm
            .AsNoTracking()
            .Include(f => f.User)
            .Include(f => f.CropTypes)
            .Include(f => f.FarmDocuments)
            .FirstOrDefaultAsync(f => f.FarmId == farmId, ct);

        if (farm is null)
            return NotFound(new { message = "Farm not found", farmId });

        // Prefer live risk calculation so score/level stay consistent with Risk Report.
        var risk = await _riskPlugin.CalculateRiskScore(farmId);
        var riskScore = risk.OverallScore > 0 ? risk.OverallScore : farm.RiskScore;
        var riskLevel = !string.IsNullOrWhiteSpace(risk.RiskLevel)
            ? risk.RiskLevel
            : GetRiskLevelLabel(riskScore);

        var dto = new FarmPublicProfileDto
        {
            FarmId = farm.FarmId,
            Name = farm.Name,
            Governorate = farm.Governorate,
            Location = farm.Location,
            Latitude = farm.Latitude,
            Longitude = farm.Longitude,
            SizeInFeddans = farm.SizeInFeddans,
            IsVerified = farm.IsVerified,
            RiskScore = riskScore,
            RiskLevel = riskLevel,
            OwnerDisplayName = ToPublicDisplayName(farm.User?.UserName),
            CropTypes = farm.CropTypes
                .Select(c => c.Name)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .OrderBy(n => n)
                .ToList(),
            Documents = farm.FarmDocuments
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new FarmPublicDocumentDto
                {
                    DocumentId = d.FarmDocumentId,
                    FileName = d.FileName,
                    FileType = d.FileType,
                    UploadedAt = d.UploadedAt,
                })
                .ToList(),
            Rating = new FarmPublicRatingSummaryDto
            {
                AverageRating = farm.AverageRating,
                RatingCount = farm.RatingCount,
            },
        };

        return Ok(dto);
    }

    private static string ToPublicDisplayName(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return "Farm owner";

        var value = userName.Trim();
        var at = value.IndexOf('@');
        if (at > 0)
            value = value[..at];

        // Never surface phone-like identifiers.
        if (value.All(ch => char.IsDigit(ch) || ch is '+' or '-' or ' '))
            return "Farm owner";

        if (value.Length == 0)
            return "Farm owner";

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private static string GetRiskLevelLabel(decimal? score) => score switch
    {
        >= 70 => "منخفض المخاطر",
        >= 40 => "متوسط المخاطر",
        null => "متوسط المخاطر",
        _ => "عالي المخاطر",
    };
}
