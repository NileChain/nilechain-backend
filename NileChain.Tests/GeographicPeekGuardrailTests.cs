using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NileChain.AI.Matching;
using NileChain.AI.Plugins;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Tests;

/// <summary>
/// Peek farms stay out of the primary shortlist until the factory opts in.
/// Exact never becomes Nationwide.
/// </summary>
public class GeographicPeekGuardrailTests
{
    [Fact]
    public async Task ExactGiza_DoesNotMixPeekFarmsIntoPrimary()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Tomato");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza Unverified", "Giza", crop, risk: 60, verified: false);
        SeedFarm(db, "Qalyubia Verified", "Qalyubia", crop, risk: 90, verified: true);
        SeedFarm(db, "Aswan Far", "Aswan", crop, risk: 99, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Single(search.Results);
        Assert.Equal("Giza", search.Results[0].Governorate);
        Assert.All(search.Results, r => Assert.False(r.IsGeographicExpansion));
        Assert.DoesNotContain(search.Results, r => r.Governorate is "Qalyubia" or "Aswan");

        Assert.NotNull(search.PeekHint);
        Assert.Equal(GeographicPeek.ReasonVerified, search.PeekHint.Reason);
        Assert.Equal("Qalyubia", search.PeekHint.Governorate);
        Assert.NotEqual("Aswan", search.PeekHint.Governorate);
    }

    [Fact]
    public async Task ExactGiza_WithoutOptIn_KeepsAswanOutOfPeek()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Wheat");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza Farm", "Giza", crop, risk: 50, verified: false);
        SeedFarm(db, "Aswan Farm", "Aswan", crop, risk: 99, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Single(search.Results);
        Assert.Equal("Giza", search.Results[0].Governorate);
        Assert.Null(search.PeekHint);
    }

    [Fact]
    public async Task ExactGiza_WithOneRingFlag_TagsQalyubiaAsExpansion()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Beans");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");
        request.FactoryApprovedOneRingExpansion = true;

        SeedFarm(db, "Giza Farm", "Giza", crop, risk: 70, verified: true);
        SeedFarm(db, "Qalyubia Farm", "Qalyubia", crop, risk: 90, verified: true);
        SeedFarm(db, "Aswan Farm", "Aswan", crop, risk: 99, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Null(search.PeekHint);
        Assert.Contains(search.Results, r => r.Governorate == "Giza" && !r.IsGeographicExpansion);
        Assert.Contains(search.Results, r => r.Governorate == "Qalyubia" && r.IsGeographicExpansion);
        Assert.DoesNotContain(search.Results, r => r.Governorate == "Aswan");
    }

    [Fact]
    public async Task NationwideOverride_StillCannotBroadenExact()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Corn");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");

        SeedFarm(db, "Giza Farm", "Giza", crop, risk: 70, verified: true);
        SeedFarm(db, "Aswan Farm", "Aswan", crop, risk: 99, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(
            request.RequestId,
            GeographicMatching.Scope.Nationwide);

        Assert.Single(search.Results);
        Assert.Equal("Giza", search.Results[0].Governorate);
    }

    [Fact]
    public async Task Nationwide_HasNoGeographicPeekHint()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Rice");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Nationwide");

        SeedFarm(db, "Giza Farm", "Giza", crop, risk: 70, verified: true);
        SeedFarm(db, "Luxor Farm", "Luxor", crop, risk: 99, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Null(search.PeekHint);
        Assert.True(search.Results.Count >= 2);
    }

    [Fact]
    public async Task ShowMore_RaisesTakeLimitWithoutChangingGeo()
    {
        await using var db = CreateDb();
        var crop = SeedCrop(db, "Mango");
        var factory = SeedFactory(db, "Giza");
        var request = SeedRequest(db, factory, crop, "Gov:Giza | GeoScope:Exact");
        request.ShortlistTakeLimit = MatchingLimits.MaxShowMoreResults;

        for (var i = 0; i < 8; i++)
            SeedFarm(db, $"Giza {i}", "Giza", crop, risk: 60 + i, verified: true);
        SeedFarm(db, "Cairo Extra", "Cairo", crop, risk: 99, verified: true);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarmsCoreAsync(request.RequestId, null);

        Assert.Equal(8, search.Results.Count);
        Assert.Equal(0, search.TruncatedCount);
        Assert.All(search.Results, r => Assert.Equal("Giza", r.Governorate));
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

    private static Factory SeedFactory(NileChainDbContext db, string governorate)
    {
        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Test Factory",
            Governorate = governorate,
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
        bool verified)
    {
        var farm = new Farm
        {
            FarmId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = name,
            Governorate = governorate,
            RiskScore = risk,
            IsVerified = verified,
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
