using NileChain.AI.Weather;
using NileChain.Application.Services;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Infrastructure.Persistence;
using NileChain.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace NileChain.Tests;

public class WeatherAndFairPriceTests
{
    [Fact]
    public void GovernorateCentroid_ResolvesGiza()
    {
        Assert.True(EgyptGovernorateCentroids.TryResolve("Giza", out var lat, out var lon));
        Assert.InRange(lat, 29, 31);
        Assert.InRange(lon, 30, 32);
    }

    [Fact]
    public void OpenMeteoClassify_HighOnHeavyRain()
    {
        var (level, reason) = OpenMeteoWeatherClient.Classify(20, 80, 32, "Giza", "2026-08-20");
        Assert.Equal("High", level);
        Assert.Contains("Open-Meteo", reason);
    }

    [Fact]
    public void OpenMeteoClassify_LowOnCalm()
    {
        var (level, _) = OpenMeteoWeatherClient.Classify(1, 10, 28, "Giza", "2026-08-20");
        Assert.Equal("Low", level);
    }

    [Fact]
    public async Task FairPriceHint_AboveMarket()
    {
        var options = new DbContextOptionsBuilder<NileChainDbContext>()
            .UseInMemoryDatabase($"fair-{Guid.NewGuid():N}")
            .Options;
        await using var db = new NileChainDbContext(options);
        var cropId = Guid.NewGuid();
        db.CropTypes.Add(new CropType { CropTypeId = cropId, Name = "Wheat" });
        db.MarketPrices.Add(new MarketPrice
        {
            PriceId = Guid.NewGuid(),
            CropTypeId = cropId,
            Governorate = "Giza",
            PricePerTon = 10000,
            Source = "Test",
            RecordedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new MarketPriceService(
            new Repository<MarketPrice>(db),
            new Repository<CropType>(db));
        var hint = await service.GetFairPriceHintAsync("Wheat", 12000, "Giza");
        Assert.True(hint.IsSuccess);
        Assert.Equal("AboveMarket", hint.Value.Alignment);
        Assert.Equal(10000, hint.Value.LatestPricePerTon);
    }
}
