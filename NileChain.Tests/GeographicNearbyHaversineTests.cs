using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NileChain.AI.Matching;
using NileChain.AI.Plugins;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Tests;

/// <summary>
/// Nearby primary filter is haversine (Matching:NearbyRadiusKm), not adjacency.
/// Missing coordinates fall back to preferred+adjacent governorates.
/// </summary>
public class GeographicNearbyHaversineTests
{
    // Giza factory center
    private const decimal FactoryLat = 30.0131m;
    private const decimal FactoryLon = 31.2089m;

    [Fact]
    public async Task Nearby_FiltersByHaversineRadius_NotAdjacencyAlone()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Wheat");
        var factory = SeedFactory(db, "Giza", FactoryLat, FactoryLon);
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Nearby");

        // Cairo city center ~5 km — inside default 50 km radius, adjacent governorate.
        SeedFarm(db, "Near Cairo", "Cairo", crop, 80, true, 30.0444m, 31.2357m);
        // Far farm in adjacent Monufia (~80+ km north) — outside 50 km with these coords.
        SeedFarm(db, "Far Monufia", "Monufia", crop, 99, true, 30.5972m, 30.9876m);
        // Distant Luxor — outside radius and not adjacent.
        SeedFarm(db, "Luxor Far", "Luxor", crop, 99, true, 25.6872m, 32.6396m);
        await db.SaveChangesAsync();

        var plugin = CreatePlugin(db, nearbyRadiusKm: 50);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Contains(search.Results, r => r.FarmName == "Near Cairo");
        Assert.DoesNotContain(search.Results, r => r.FarmName == "Far Monufia");
        Assert.DoesNotContain(search.Results, r => r.FarmName == "Luxor Far");

        var near = search.Results.Single(r => r.FarmName == "Near Cairo");
        Assert.False(near.UsedGovernorateFallback);
        Assert.NotNull(near.DistanceKm);
        Assert.True(near.DistanceKm < 50);
    }

    [Fact]
    public async Task Nearby_MissingFarmCoordinates_FallsBackToGovernorateAdjacency()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Corn");
        var factory = SeedFactory(db, "Giza", FactoryLat, FactoryLon);
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Nearby");

        // No lat/long — must use governorate fallback (Cairo is adjacent to Giza).
        SeedFarm(db, "Cairo NoCoords", "Cairo", crop, 75, true, lat: null, lon: null);
        SeedFarm(db, "Luxor NoCoords", "Luxor", crop, 99, true, lat: null, lon: null);
        await db.SaveChangesAsync();

        var plugin = CreatePlugin(db, nearbyRadiusKm: 50);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Contains(search.Results, r => r.FarmName == "Cairo NoCoords");
        Assert.DoesNotContain(search.Results, r => r.FarmName == "Luxor NoCoords");

        var cairo = search.Results.Single(r => r.FarmName == "Cairo NoCoords");
        Assert.True(cairo.UsedGovernorateFallback);
        Assert.Null(cairo.DistanceKm);
    }

    [Fact]
    public async Task Nearby_MissingFactoryCoordinates_FallsBackToGovernorateAdjacency()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Beans");
        var factory = SeedFactory(db, "Giza", lat: null, lon: null);
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Nearby");

        SeedFarm(db, "Giza Farm", "Giza", crop, 80, true, 30.01m, 31.20m);
        SeedFarm(db, "Cairo Farm", "Cairo", crop, 75, true, 30.04m, 31.23m);
        SeedFarm(db, "Luxor Farm", "Luxor", crop, 99, true, 25.68m, 32.63m);
        await db.SaveChangesAsync();

        var plugin = CreatePlugin(db, nearbyRadiusKm: 50);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Equal(2, search.Results.Count);
        Assert.Contains(search.Results, r => r.Governorate == "Giza");
        Assert.Contains(search.Results, r => r.Governorate == "Cairo");
        Assert.DoesNotContain(search.Results, r => r.Governorate == "Luxor");
        Assert.All(search.Results, r => Assert.True(r.UsedGovernorateFallback));
        Assert.All(search.Results, r => Assert.Null(r.DistanceKm));
    }

    [Fact]
    public async Task Exact_StillIgnoresDistance_EvenWhenFarmsHaveCoordinates()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Rice");
        var factory = SeedFactory(db, "Giza", FactoryLat, FactoryLon);
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza Only", "Giza", crop, 70, true, 30.01m, 31.20m);
        // Within 50 km but Cairo — Exact must exclude.
        SeedFarm(db, "Cairo Close", "Cairo", crop, 99, true, 30.0444m, 31.2357m);
        await db.SaveChangesAsync();

        var plugin = CreatePlugin(db, nearbyRadiusKm: 50);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Single(search.Results);
        Assert.Equal("Giza", search.Results[0].Governorate);
        Assert.False(search.Results[0].UsedGovernorateFallback);
    }

    private static MatchingPlugin CreatePlugin(NileChainDbContext db, double nearbyRadiusKm)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Matching:NearbyRadiusKm"] = nearbyRadiusKm.ToString(System.Globalization.CultureInfo.InvariantCulture)
            })
            .Build();
        return new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance, config);
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
        decimal? lat,
        decimal? lon)
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
        decimal? lat,
        decimal? lon)
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
