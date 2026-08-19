using NileChain.AI.Matching;
using NileChain.AI.Models;

namespace NileChain.Tests;

public class GeographicPeekTests
{
    [Fact]
    public void TryPeekParameters_Exact_UsesNearbyNotNationwide()
    {
        Assert.True(GeographicPeek.TryPeekParameters(
            GeographicMatching.Scope.Exact, 50, out var scope, out var radius));
        Assert.Equal(GeographicMatching.Scope.Nearby, scope);
        Assert.Equal(50, radius);
    }

    [Fact]
    public void TryPeekParameters_Nearby_AddsOneRingKm()
    {
        Assert.True(GeographicPeek.TryPeekParameters(
            GeographicMatching.Scope.Nearby, 50, out var scope, out var radius));
        Assert.Equal(GeographicMatching.Scope.Nearby, scope);
        Assert.Equal(75, radius);
    }

    [Fact]
    public void TryPeekParameters_Nationwide_HasNoGeoPeek()
    {
        Assert.False(GeographicPeek.TryPeekParameters(
            GeographicMatching.Scope.Nationwide, 50, out _, out _));
    }

    [Fact]
    public void Pick_EmptyPrimary_UsesClosestOutsideFarm()
    {
        var hint = GeographicPeek.Pick(
            [],
            [
                Farm("far", score: 99, distance: 40),
                Farm("near", score: 60, distance: 12)
            ]);

        Assert.NotNull(hint);
        Assert.Equal(GeographicPeek.ReasonEmptyPrimary, hint.Reason);
        Assert.Equal("near", hint.FarmName);
    }

    [Fact]
    public void Pick_VerifiedOutside_WhenPrimaryUnverified()
    {
        var hint = GeographicPeek.Pick(
            [Farm("in", score: 80, verified: false, distance: 8)],
            [
                Farm("in", score: 80, verified: false, distance: 8),
                Farm("out", score: 70, verified: true, distance: 22)
            ]);

        Assert.NotNull(hint);
        Assert.Equal(GeographicPeek.ReasonVerified, hint.Reason);
        Assert.Equal("out", hint.FarmName);
    }

    [Fact]
    public void Pick_HigherScore_RequiresDelta()
    {
        var primary = Farm("in", score: 70, distance: 10);
        var weak = Farm("weak", score: 75, distance: 18);
        var strong = Farm("strong", score: 82, distance: 20);

        Assert.Null(GeographicPeek.Pick([primary], [primary, weak]));

        var hint = GeographicPeek.Pick([primary], [primary, strong]);
        Assert.NotNull(hint);
        Assert.Equal(GeographicPeek.ReasonHigherScore, hint.Reason);
        Assert.Equal("strong", hint.FarmName);
    }

    [Fact]
    public void Pick_HigherTrust_RequiresDelta()
    {
        var hint = GeographicPeek.Pick(
            [Farm("in", score: 80, risk: 50, distance: 10)],
            [
                Farm("in", score: 80, risk: 50, distance: 10),
                Farm("out", score: 78, risk: 70, distance: 18)
            ]);

        Assert.NotNull(hint);
        Assert.Equal(GeographicPeek.ReasonHigherTrust, hint.Reason);
        Assert.Equal("out", hint.FarmName);
    }

    [Fact]
    public void Pick_DoesNotBannerDistantJump()
    {
        var hint = GeographicPeek.Pick(
            [Farm("qalyubia", score: 70, verified: false, distance: 12)],
            [
                Farm("qalyubia", score: 70, verified: false, distance: 12),
                Farm("aswan", score: 99, verified: true, distance: 700)
            ]);

        Assert.Null(hint);
    }

    [Fact]
    public void Pick_DoesNotMixShadowIntoSilenceWhenNotBetter()
    {
        var hint = GeographicPeek.Pick(
            [Farm("in", score: 90, verified: true, risk: 80, distance: 8)],
            [
                Farm("in", score: 90, verified: true, risk: 80, distance: 8),
                Farm("out", score: 91, verified: true, risk: 81, distance: 20)
            ]);

        Assert.Null(hint);
    }

    private static MatchResult Farm(
        string name,
        decimal score,
        decimal risk = 70,
        bool verified = false,
        double? distance = null)
    {
        return new MatchResult
        {
            FarmId = Guid.NewGuid(),
            FarmName = name,
            Governorate = name,
            MatchScore = score,
            RiskScore = risk,
            IsVerified = verified,
            DistanceKm = distance
        };
    }
}
