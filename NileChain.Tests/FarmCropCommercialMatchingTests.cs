using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NileChain.AI.Plugins;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Tests;

public class FarmCropCommercialMatchingTests
{
    [Fact]
    public async Task Matching_ExcludesFarm_WhenAvailableQuantityBelowDemand()
    {
        await using var db = CreateDb();
        var crop = new CropType { CropTypeId = Guid.NewGuid(), Name = "Wheat" };
        db.CropTypes.Add(crop);

        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Buyer Co",
            Governorate = "Cairo",
            CreatedAt = DateTime.UtcNow
        };
        db.Factory.Add(factory);

        var enough = SeedFarm(db, "Enough Farm", crop, availableTons: 80m);
        var shortFarm = SeedFarm(db, "Short Farm", crop, availableTons: 10m);

        var request = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = crop.CropTypeId,
            QuantityTons = 50m,
            PricePerTon = 12000m,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(30),
            Status = SupplyRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            QualitySpecs = "GeoScope:Nationwide"
        };
        db.SupplyRequests.Add(request);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var result = await plugin.FindMatchingFarms(request.RequestId);

        Assert.Contains(result.Results, r => r.FarmId == enough.FarmId);
        Assert.DoesNotContain(result.Results, r => r.FarmId == shortFarm.FarmId);
    }

    [Fact]
    public async Task Matching_ExcludesFarm_WhenMinPriceAboveOffer()
    {
        await using var db = CreateDb();
        var crop = new CropType { CropTypeId = Guid.NewGuid(), Name = "Corn" };
        db.CropTypes.Add(crop);

        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Mill",
            Governorate = "Giza",
            CreatedAt = DateTime.UtcNow
        };
        db.Factory.Add(factory);

        var affordable = SeedFarm(db, "Affordable", crop, minPrice: 9000m);
        var expensive = SeedFarm(db, "Expensive", crop, minPrice: 15000m);

        var request = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = crop.CropTypeId,
            QuantityTons = 20m,
            PricePerTon = 11000m,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(20),
            Status = SupplyRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            QualitySpecs = "GeoScope:Nationwide"
        };
        db.SupplyRequests.Add(request);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var result = await plugin.FindMatchingFarms(request.RequestId);

        Assert.Contains(result.Results, r => r.FarmId == affordable.FarmId);
        Assert.DoesNotContain(result.Results, r => r.FarmId == expensive.FarmId);
    }

    private static NileChainDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NileChainDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NileChainDbContext(options);
    }

    private static Farm SeedFarm(
        NileChainDbContext db,
        string name,
        CropType crop,
        decimal? availableTons = 100m,
        decimal? minPrice = 8000m)
    {
        var farm = new Farm
        {
            FarmId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = name,
            Governorate = "Cairo",
            RiskScore = 70m,
            IsVerified = true,
            ProfileComplete = true,
            CreatedAt = DateTime.UtcNow,
            FarmCrops =
            [
                new FarmCrop
                {
                    CropTypeId = crop.CropTypeId,
                    CropType = crop,
                    AvailableQuantityTons = availableTons,
                    AvailableFrom = DateTime.UtcNow.Date.AddMonths(-1),
                    AvailableTo = DateTime.UtcNow.Date.AddMonths(6),
                    MinPricePerTon = minPrice
                }
            ]
        };
        db.Farm.Add(farm);
        return farm;
    }
}
