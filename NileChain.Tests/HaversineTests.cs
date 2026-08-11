using NileChain.Domain.Common;

namespace NileChain.Tests;

public class HaversineTests
{
    // Approximate published distances (allow ±3% / a few km for city-center coords).
    [Theory]
    [InlineData(30.0444, 31.2357, 31.2001, 29.9187, 179.0)] // Cairo ↔ Alexandria
    [InlineData(30.0131, 31.2089, 30.0444, 31.2357, 4.3)]   // Giza ↔ Cairo (city centers)
    [InlineData(30.0444, 31.2357, 25.6872, 32.6396, 505.0)] // Cairo ↔ Luxor
    public void DistanceKm_KnownCityPairs_WithinTolerance(
        double lat1, double lon1, double lat2, double lon2, double expectedKm)
    {
        var actual = Haversine.DistanceKm(lat1, lon1, lat2, lon2);
        Assert.InRange(actual, expectedKm * 0.97, expectedKm * 1.03);
    }

    [Fact]
    public void DistanceKm_SamePoint_IsZero()
    {
        Assert.Equal(0, Haversine.DistanceKm(30.0, 31.0, 30.0, 31.0), precision: 6);
    }

    [Fact]
    public void DistanceKm_IsSymmetric()
    {
        var a = Haversine.DistanceKm(30.0444, 31.2357, 31.2001, 29.9187);
        var b = Haversine.DistanceKm(31.2001, 29.9187, 30.0444, 31.2357);
        Assert.Equal(a, b, precision: 6);
    }
}
