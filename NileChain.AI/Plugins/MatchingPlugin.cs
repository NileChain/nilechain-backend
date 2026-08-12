using NileChain.AI.Models;
using NileChain.Domain.Common;
using NileChain.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;
using NileChain.AI.Matching;

namespace NileChain.AI.Plugins;

public class MatchingPlugin
{
    private const decimal CropMatchPoints = 40m;
    private const decimal LocationMatchPoints = 20m;
    private const decimal VerifiedFarmPoints = 20m;
    private const decimal RiskScoreMaxPoints = 20m;

    private readonly NileChainDbContext _context;
    private readonly ILogger<MatchingPlugin> _logger;
    private readonly int _maxResults;
    private readonly double _nearbyRadiusKm;

    public MatchingPlugin(
        NileChainDbContext context,
        ILogger<MatchingPlugin> logger,
        IConfiguration? configuration = null)
    {
        _context = context;
        _logger = logger;
        _maxResults = MatchingLimits.ResolveMaxResults(
            configuration?.GetValue<int?>("Matching:MaxResults"));
        _nearbyRadiusKm = MatchingLimits.ResolveNearbyRadiusKm(
            configuration?.GetValue<double?>("Matching:NearbyRadiusKm"));
    }

    /// <summary>Configured Nearby haversine radius (not orchestration radiusKm).</summary>
    public double NearbyRadiusKm => _nearbyRadiusKm;

    [KernelFunction("find_matching_farms")]
    [Description("Searches the database for farms that match the supply request criteria")]
    public async Task<MatchSearchResult> FindMatchingFarms(
        [Description("The supply request ID")] Guid requestId)
    {
        return await FindMatchingFarmsCoreAsync(requestId, geographicOverride: null);
    }

    /// <summary>
    /// Core matching with optional geographic scope override.
    /// Persisted Exact/Nearby scope is authoritative — overrides cannot broaden it.
    /// </summary>
    public async Task<MatchSearchResult> FindMatchingFarmsCoreAsync(
        Guid requestId,
        GeographicMatching.Scope? geographicOverride)
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

        var preferredGovernorates = GeographicMatching.ParsePreferredGovernorates(
            supplyRequest.QualitySpecs,
            supplyRequest.Factory?.Governorate);

        var persistedScope = GeographicMatching.ParseScope(
            supplyRequest.QualitySpecs,
            preferredGovernorates.Count > 0);

        var scope = GeographicMatching.ResolveEffectiveScope(persistedScope, geographicOverride);

        var neededTons = supplyRequest.QuantityTons;
        var deliveryDate = supplyRequest.DeliveryDate?.Date;
        var offeredPrice = supplyRequest.PricePerTon;

        // Exclude farms whose linked user is inactive (Users.IsActive == false).
        var inactiveUserIds = await _context.Users
            .AsNoTracking()
            .Where(u => !u.IsActive)
            .Select(u => u.Id)
            .ToListAsync();

        // Capacity / season / floor price: null commercial fields stay eligible (unknown).
        // Factory-excluded farms for this request never re-enter the shortlist.
        var excludedFarmIds = await _context.FarmMatches
            .AsNoTracking()
            .Where(m => m.RequestId == requestId && m.IsExcludedByFactory)
            .Select(m => m.FarmId)
            .ToListAsync();

        var candidates = await _context.Farm
            .AsNoTracking()
            .Where(f => f.FarmCrops.Any(fc =>
                fc.CropTypeId == cropTypeId
                && (fc.AvailableQuantityTons == null || fc.AvailableQuantityTons >= neededTons)
                && (deliveryDate == null
                    || fc.AvailableFrom == null
                    || fc.AvailableFrom <= deliveryDate)
                && (deliveryDate == null
                    || fc.AvailableTo == null
                    || fc.AvailableTo >= deliveryDate)
                && (offeredPrice == null
                    || fc.MinPricePerTon == null
                    || fc.MinPricePerTon <= offeredPrice)))
            .Where(f => !inactiveUserIds.Contains(f.UserId))
            .Where(f => !excludedFarmIds.Contains(f.FarmId))
            .Select(f => new FarmCandidate(
                f.FarmId,
                f.Name,
                f.Governorate,
                f.RiskScore,
                f.IsVerified,
                f.Latitude,
                f.Longitude,
                f.FarmCrops.Select(c => c.CropType.Name).ToList()))
            .ToListAsync();

        var beforeGeo = candidates.Count;
        var factoryLat = supplyRequest.Factory?.Latitude;
        var factoryLon = supplyRequest.Factory?.Longitude;

        var geoFiltered = ApplyGeographicFilter(
            candidates,
            preferredGovernorates,
            scope,
            factoryLat,
            factoryLon);

        _logger.LogInformation(
            "Geographic matching RequestId={RequestId} SelectedGovernorates={Preferred} " +
            "PersistedGeoScope={Persisted} RequestedOverride={Override} EffectiveGeoScope={Effective} " +
            "NearbyRadiusKm={NearbyRadiusKm} " +
            "CandidateCountBeforeGeoFilter={Before} CandidateCountAfterGeoFilter={After} " +
            "ExpansionAttempted={ExpansionAttempted} ExpansionAllowed={ExpansionAllowed}",
            requestId,
            string.Join(",", preferredGovernorates),
            persistedScope,
            geographicOverride?.ToString() ?? "none",
            scope,
            _nearbyRadiusKm,
            beforeGeo,
            geoFiltered.Count,
            geographicOverride is not null && (int)geographicOverride.Value > (int)persistedScope,
            GeographicMatching.AllowsAutomaticGeographicExpansion(persistedScope)
                || (geographicOverride is not null
                    && (int)geographicOverride.Value <= (int)persistedScope));

        var ranked = geoFiltered
            .Select(item =>
            {
                var farm = item.Candidate;
                var riskScore = farm.RiskScore ?? 0m;
                var matchScore = CalculateMatchScore(
                    farm.Governorate,
                    preferredGovernorates,
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
                    DistanceKm = item.DistanceKm,
                    UsedGovernorateFallback = item.UsedGovernorateFallback,
                    CropTypes = farm.CropTypeNames.Count > 0
                        ? farm.CropTypeNames
                        : new List<string> { cropTypeName }
                };
            })
            .OrderByDescending(r => r.MatchScore)
            .ThenBy(r => r.DistanceKm ?? double.MaxValue)
            .ThenByDescending(r => r.RiskScore)
            .ThenByDescending(r => r.IsVerified)
            .ThenBy(r => r.FarmId)
            .ToList();

        var totalEligible = ranked.Count;
        var results = ranked.Take(_maxResults).ToList();
        var truncatedCount = Math.Max(0, totalEligible - results.Count);

        return new MatchSearchResult
        {
            Results = results,
            TotalEligible = totalEligible,
            TruncatedCount = truncatedCount,
            TakeLimit = _maxResults
        };
    }

    /// <summary>
    /// Pure ranking helper for unit tests (stable FarmId tie-break after scores).
    /// </summary>
    public static IReadOnlyList<MatchResult> RankAndTake(
        IEnumerable<MatchResult> candidates,
        int maxResults = MatchingLimits.DefaultMaxResults)
    {
        var limit = MatchingLimits.ResolveMaxResults(maxResults);
        var ranked = candidates
            .OrderByDescending(r => r.MatchScore)
            .ThenBy(r => r.DistanceKm ?? double.MaxValue)
            .ThenByDescending(r => r.RiskScore)
            .ThenByDescending(r => r.IsVerified)
            .ThenBy(r => r.FarmId)
            .ToList();

        return ranked.Take(limit).ToList();
    }

    private List<GeoFilteredCandidate> ApplyGeographicFilter(
        IReadOnlyList<FarmCandidate> candidates,
        IReadOnlyList<string> preferredGovernorates,
        GeographicMatching.Scope scope,
        decimal? factoryLat,
        decimal? factoryLon)
    {
        if (scope == GeographicMatching.Scope.Exact
            || scope == GeographicMatching.Scope.Nationwide)
        {
            return candidates
                .Where(f => GeographicMatching.IsEligible(
                    f.Governorate, preferredGovernorates, scope))
                .Select(f => new GeoFilteredCandidate(
                    f,
                    TryDistance(factoryLat, factoryLon, f.Latitude, f.Longitude),
                    UsedGovernorateFallback: false))
                .ToList();
        }

        // Nearby: haversine when both ends have coords; else governorate adjacency fallback.
        var factoryHasCoords = factoryLat is not null && factoryLon is not null;
        var accepted = new List<GeoFilteredCandidate>();

        foreach (var farm in candidates)
        {
            var farmHasCoords = farm.Latitude is not null && farm.Longitude is not null;

            if (factoryHasCoords && farmHasCoords)
            {
                var distance = Haversine.DistanceKm(
                    factoryLat!.Value,
                    factoryLon!.Value,
                    farm.Latitude!.Value,
                    farm.Longitude!.Value);

                if (distance <= _nearbyRadiusKm)
                {
                    accepted.Add(new GeoFilteredCandidate(
                        farm,
                        distance,
                        UsedGovernorateFallback: false));
                }

                continue;
            }

            // Missing factory and/or farm coordinates → governorate-only Nearby (adjacency).
            if (GeographicMatching.IsEligible(
                    farm.Governorate, preferredGovernorates, GeographicMatching.Scope.Nearby))
            {
                accepted.Add(new GeoFilteredCandidate(
                    farm,
                    DistanceKm: null,
                    UsedGovernorateFallback: true));
            }
        }

        return accepted;
    }

    private static double? TryDistance(
        decimal? factoryLat,
        decimal? factoryLon,
        decimal? farmLat,
        decimal? farmLon)
    {
        if (factoryLat is null || factoryLon is null || farmLat is null || farmLon is null)
            return null;

        return Haversine.DistanceKm(
            factoryLat.Value,
            factoryLon.Value,
            farmLat.Value,
            farmLon.Value);
    }

    private static decimal CalculateMatchScore(
        string? farmGovernorate,
        IReadOnlyList<string> preferredGovernorates,
        bool isVerified,
        decimal riskScore)
    {
        // Crop match is guaranteed by the candidate query.
        decimal score = CropMatchPoints;

        if (GeographicMatching.IsPreferredMatch(farmGovernorate, preferredGovernorates))
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

    private sealed record FarmCandidate(
        Guid FarmId,
        string Name,
        string? Governorate,
        decimal? RiskScore,
        bool IsVerified,
        decimal? Latitude,
        decimal? Longitude,
        List<string> CropTypeNames);

    private sealed record GeoFilteredCandidate(
        FarmCandidate Candidate,
        double? DistanceKm,
        bool UsedGovernorateFallback);
}
