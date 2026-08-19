using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NileChain.AI.Plugins;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Tests;

public class WedgeMatcherTests
{
    [Fact]
    public async Task Exact_Tomato_Qalyubia_HasAtLeastThreeEligible()
    {
        await using var db = CreateDb();
        var tomato = new CropType { CropTypeId = Guid.NewGuid(), Name = "Tomato" };
        db.CropTypes.Add(tomato);

        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Banha Paste [WEDGE]",
            Governorate = "Qalyubia",
            CreatedAt = DateTime.UtcNow
        };
        db.Factory.Add(factory);

        for (var i = 1; i <= 20; i++)
        {
            db.Farm.Add(new Farm
            {
                FarmId = Guid.NewGuid(),
                UserId = Guid.NewGuid(),
                Name = $"Qalyubia Tomato {i:D2}",
                Governorate = "Qalyubia",
                IsVerified = true,
                ProfileComplete = true,
                RiskScore = 70m + i,
                CreatedAt = DateTime.UtcNow,
                FarmCrops =
                [
                    new FarmCrop
                    {
                        CropTypeId = tomato.CropTypeId,
                        CropType = tomato,
                        AvailableQuantityTons = 80m + i,
                        AvailableFrom = DateTime.UtcNow.Date.AddMonths(-2),
                        AvailableTo = DateTime.UtcNow.Date.AddMonths(6),
                        MinPricePerTon = 6200m,
                        IsPublished = true
                    }
                ]
            });
        }

        db.Farm.Add(new Farm
        {
            FarmId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Name = "Giza decoy",
            Governorate = "Giza",
            IsVerified = true,
            ProfileComplete = true,
            RiskScore = 99m,
            CreatedAt = DateTime.UtcNow,
            FarmCrops =
            [
                new FarmCrop
                {
                    CropTypeId = tomato.CropTypeId,
                    CropType = tomato,
                    AvailableQuantityTons = 200m,
                    MinPricePerTon = 5000m,
                    IsPublished = true
                }
            ]
        });

        var request = new SupplyRequest
        {
            RequestId = Guid.NewGuid(),
            FactoryId = factory.FactoryId,
            CropTypeId = tomato.CropTypeId,
            QuantityTons = 40m,
            PricePerTon = 8000m,
            DeliveryDate = DateTime.UtcNow.Date.AddDays(45),
            Status = SupplyRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            QualitySpecs = "[WEDGE]:REQ-EXACT | Gov:Qalyubia | GeoScope:Exact | Processing grade paste"
        };
        db.SupplyRequests.Add(request);
        await db.SaveChangesAsync();

        var plugin = new MatchingPlugin(db, NullLogger<MatchingPlugin>.Instance);
        var search = await plugin.FindMatchingFarms(request.RequestId);

        Assert.True(search.TotalEligible >= 3, $"Expected ≥3 eligible, got {search.TotalEligible}");
        Assert.All(search.Results, r => Assert.Equal("Qalyubia", r.Governorate));
    }

    private static NileChainDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<NileChainDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new NileChainDbContext(options);
    }
}
