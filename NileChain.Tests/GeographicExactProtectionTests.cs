using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NileChain.AI.Matching;
using NileChain.AI.Models;
using NileChain.AI.Orchestration;
using NileChain.AI.Plugins;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;
using System.Text.Json;

namespace NileChain.Tests;

/// <summary>
/// Exact must stay Exact — coverage for MatchingPlugin + orchestration tool overrides.
/// </summary>
public class GeographicExactProtectionTests
{
    [Fact]
    public void ResolveEffectiveScope_NeverBroadensExact()
    {
        Assert.Equal(
            GeographicMatching.Scope.Exact,
            GeographicMatching.ResolveEffectiveScope(
                GeographicMatching.Scope.Exact,
                GeographicMatching.Scope.Nationwide));

        Assert.Equal(
            GeographicMatching.Scope.Exact,
            GeographicMatching.ResolveEffectiveScope(
                GeographicMatching.Scope.Exact,
                GeographicMatching.Scope.Nearby));
    }

    [Fact]
    public void ResolveEffectiveScope_NeverBroadensNearbyToNationwide()
    {
        Assert.Equal(
            GeographicMatching.Scope.Nearby,
            GeographicMatching.ResolveEffectiveScope(
                GeographicMatching.Scope.Nearby,
                GeographicMatching.Scope.Nationwide));
    }

    [Fact]
    public void AllowsAutomaticGeographicExpansion_OnlyNationwide()
    {
        Assert.False(GeographicMatching.AllowsAutomaticGeographicExpansion(GeographicMatching.Scope.Exact));
        Assert.False(GeographicMatching.AllowsAutomaticGeographicExpansion(GeographicMatching.Scope.Nearby));
        Assert.True(GeographicMatching.AllowsAutomaticGeographicExpansion(GeographicMatching.Scope.Nationwide));
    }

    [Fact]
    public async Task Test1_ExactGiza_ReturnsOnlyGiza_EvenWhenDistantFarmsExist()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Wheat");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza Farm", "Giza", crop, risk: 80, verified: true);
        SeedFarm(db, "NV1", "New Valley", crop, risk: 90, verified: true);
        SeedFarm(db, "NV2", "New Valley", crop, risk: 88, verified: true);
        SeedFarm(db, "NV3", "New Valley", crop, risk: 85, verified: true);
        SeedFarm(db, "SS1", "South Sinai", crop, risk: 92, verified: true);
        SeedFarm(db, "SS2", "South Sinai", crop, risk: 91, verified: true);
        SeedFarm(db, "Minya1", "Minya", crop, risk: 87, verified: true);
        SeedFarm(db, "Minya2", "Minya", crop, risk: 86, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, geographicOverride: null);
        var results = search.Results;

        Assert.Single(results);
        Assert.Equal("Giza", results[0].Governorate);
        Assert.Equal("Giza Farm", results[0].FarmName);
    }

    [Fact]
    public async Task Test2_ExactGiza_ZeroGizaFarms_ReturnsEmpty_NotNationwide()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Tomato");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "NV1", "New Valley", crop, risk: 90, verified: true);
        SeedFarm(db, "SS1", "South Sinai", crop, risk: 90, verified: true);
        SeedFarm(db, "Minya1", "Minya", crop, risk: 90, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Empty(search.Results);
    }

    [Fact]
    public async Task Test3_ExactGiza_TwoGizaFarms_DoesNotPadToThreeOrFive()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Beans");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza A", "Giza", crop, risk: 70, verified: true);
        SeedFarm(db, "Giza B", "Giza", crop, risk: 65, verified: true);
        SeedFarm(db, "Cairo X", "Cairo", crop, risk: 99, verified: true);
        SeedFarm(db, "Luxor X", "Luxor", crop, risk: 99, verified: true);
        SeedFarm(db, "Qena X", "Qena", crop, risk: 99, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);
        var results = search.Results;

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("Giza", r.Governorate));
    }

    [Fact]
    public async Task Test4_NearbyGiza_UsesHaversine_NotAdjacencyAlone()
    {
        // DELIBERATE UPDATE (haversine Nearby): previously asserted adjacency-only
        // (Cairo in, Luxor/New Valley out) with no coordinates. Nearby now filters by
        // Matching:NearbyRadiusKm when coords exist; adjacency remains the missing-coords fallback.
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Corn");
        var factory = SeedFactory(db, "Giza", lat: 30.0131m, lon: 31.2089m);
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Nearby");

        SeedFarm(db, "Giza Farm", "Giza", crop, risk: 80, verified: true, lat: 30.01m, lon: 31.20m);
        SeedFarm(db, "Cairo Farm", "Cairo", crop, risk: 75, verified: true, lat: 30.0444m, lon: 31.2357m);
        SeedFarm(db, "Luxor Farm", "Luxor", crop, risk: 99, verified: true, lat: 25.6872m, lon: 32.6396m);
        SeedFarm(db, "New Valley Farm", "New Valley", crop, risk: 99, verified: true, lat: 25.4510m, lon: 30.5460m);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);
        var results = search.Results;

        Assert.Equal(2, results.Count);
        Assert.Contains(results, r => r.Governorate == "Giza");
        Assert.Contains(results, r => r.Governorate == "Cairo");
        Assert.DoesNotContain(results, r => r.Governorate == "Luxor");
        Assert.DoesNotContain(results, r => r.Governorate == "New Valley");
        Assert.All(results, r => Assert.False(r.UsedGovernorateFallback));
        Assert.All(results, r => Assert.NotNull(r.DistanceKm));
    }

    [Fact]
    public async Task Test5_NationwideGiza_AllowsDistantGovernorates()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Rice");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Nationwide");

        SeedFarm(db, "Giza Farm", "Giza", crop, risk: 70, verified: true);
        SeedFarm(db, "Luxor Farm", "Luxor", crop, risk: 90, verified: true);
        SeedFarm(db, "Minya Farm", "Minya", crop, risk: 85, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);
        var results = search.Results;

        Assert.True(results.Count >= 3);
        Assert.Contains(results, r => r.Governorate == "Luxor");
        Assert.Contains(results, r => r.Governorate == "Minya");
    }

    [Fact]
    public async Task Test6_SearchFarms_Radius100_DoesNotOverrideExactToNationwide()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Mango");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza Mango", "Giza", crop, risk: 70, verified: true);
        SeedFarm(db, "New Valley Mango", "New Valley", crop, risk: 99, verified: true);
        SeedFarm(db, "South Sinai Mango", "South Sinai", crop, risk: 98, verified: true);
        await db.SaveChangesAsync();

        var state = new OrchestrationRunState
        {
            RequestId = request.RequestId,
            Request = new AgentRequest
            {
                RequestId = request.RequestId,
                CropType = "Mango",
                FactoryGovernorate = "Giza",
                QualitySpecs = "Gov:Giza | GeoScope:Exact"
            }
        };

        var tools = new OrchestrationToolsPlugin(
            new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance),
            riskPlugin: null!,
            contractAgent: null!,
            db,
            NullLogger.Instance,
            state);

        var json = await tools.SearchFarms("Mango", "Giza", "Gov:Giza | GeoScope:Exact", radiusKm: 100);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal("Exact", root.GetProperty("geographicScope").GetString());
        Assert.Equal("Exact", root.GetProperty("persistedGeographicScope").GetString());
        Assert.False(root.GetProperty("expandedNationwide").GetBoolean());
        Assert.False(root.GetProperty("expansionAllowed").GetBoolean());
        Assert.Equal(1, root.GetProperty("count").GetInt32());

        Assert.Single(state.RankedCandidates);
        Assert.Equal("Giza", state.RankedCandidates[0].Governorate);
        Assert.DoesNotContain(state.RankedCandidates, c => c.Governorate is "New Valley" or "South Sinai");
    }

    [Fact]
    public async Task Test7_WidenSearchRadius_Exact_IsBlocked()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Orange");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");
        await db.SaveChangesAsync();

        var state = new OrchestrationRunState
        {
            RequestId = request.RequestId,
            Request = new AgentRequest
            {
                RequestId = request.RequestId,
                CropType = "Orange",
                FactoryGovernorate = "Giza",
                QualitySpecs = "Gov:Giza | GeoScope:Exact"
            }
        };

        var tools = new OrchestrationToolsPlugin(
            new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance),
            riskPlugin: null!,
            contractAgent: null!,
            db,
            NullLogger.Instance,
            state);

        var json = await tools.WidenSearchRadius(50);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.GetProperty("blocked").GetBoolean());
        Assert.False(root.GetProperty("expansionAllowed").GetBoolean());
        Assert.Contains("Exact", root.GetProperty("reason").GetString(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, state.WidenCallCount);
        Assert.Equal(OrchestrationRunState.DefaultRadiusKm, state.CurrentRadiusKm);
    }

    [Fact]
    public async Task MatchingPlugin_IgnoresNationwideOverride_WhenPersistedExact()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Cucumber");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza Only", "Giza", crop, risk: 60, verified: true);
        SeedFarm(db, "Qena Far", "Qena", crop, risk: 99, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(
            request.RequestId,
            GeographicMatching.Scope.Nationwide);
        var results = search.Results;

        Assert.Single(results);
        Assert.Equal("Giza", results[0].Governorate);
    }

    [Fact]
    public async Task MergeIntoRanked_PurgesStaleNationwideCandidates_OnExactSearch()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Sugarcane");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza Sugar", "Giza", crop, risk: 70, verified: true);
        await db.SaveChangesAsync();

        var state = new OrchestrationRunState
        {
            RequestId = request.RequestId,
            Request = new AgentRequest
            {
                RequestId = request.RequestId,
                CropType = "Sugarcane",
                FactoryGovernorate = "Giza",
                QualitySpecs = "Gov:Giza | GeoScope:Exact"
            },
            RankedCandidates =
            [
                new MatchResult
                {
                    FarmId = Guid.NewGuid(),
                    FarmName = "Stale New Valley",
                    Governorate = "New Valley",
                    MatchScore = 99,
                    RiskScore = 99,
                    IsVerified = true
                }
            ]
        };

        var tools = new OrchestrationToolsPlugin(
            new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance),
            riskPlugin: null!,
            contractAgent: null!,
            db,
            NullLogger.Instance,
            state);

        await tools.SearchFarms("Sugarcane", "Giza", "Gov:Giza | GeoScope:Exact", radiusKm: 50);

        Assert.DoesNotContain(state.RankedCandidates, c => c.Governorate == "New Valley");
        Assert.All(state.RankedCandidates, c => Assert.Equal("Giza", c.Governorate));
    }

    private static NileChainDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NileChainDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NileChainDbContext(options);
    }

    private static CropType SeedCrop(NileChainDbContext db, string name)
    {
        var crop = new CropType { CropTypeId = Guid.NewGuid(), Name = name };
        db.CropTypes.Add(crop);
        return crop;
    }

    private static Factory SeedFactory(
        NileChainDbContext db,
        string governorate,
        decimal? lat = null,
        decimal? lon = null)
    {
        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Test Factory",
            Governorate = governorate,
            Latitude = lat,
            Longitude = lon,
            CreatedAt = DateTime.UtcNow
        };
        db.Factory.Add(factory);
        return factory;
    }

    private static SupplyRequest SeedRequest(
        NileChainDbContext db,
        Factory factory,
        CropType crop,
        string qualitySpecs)
    {
        var request = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = crop.CropTypeId,
            QuantityTons = 10,
            QualitySpecs = qualitySpecs,
            Status = SupplyRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.SupplyRequests.Add(request);
        return request;
    }

    private static Farm SeedFarm(
        NileChainDbContext db,
        string name,
        string governorate,
        CropType crop,
        decimal risk,
        bool verified,
        decimal? lat = null,
        decimal? lon = null)
    {
        var farm = new Farm
        {
            FarmId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = name,
            Governorate = governorate,
            RiskScore = risk,
            IsVerified = verified,
            Latitude = lat,
            Longitude = lon,
            ProfileComplete = true,
            CreatedAt = DateTime.UtcNow,
            FarmCrops = new List<FarmCrop>
            {
                new()
                {
                    CropTypeId = crop.CropTypeId,
                    CropType = crop,
                    AvailableQuantityTons = 100m
                }
            }
        };
        db.Farm.Add(farm);
        return farm;
    }
}
