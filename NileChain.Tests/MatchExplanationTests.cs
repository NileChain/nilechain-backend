using NileChain.Domain.Matching;

namespace NileChain.Tests;

public class MatchScoreWeightsTests
{
    [Fact]
    public void Weights_SumTo100()
    {
        Assert.Equal(
            MatchScoreWeights.Max,
            MatchScoreWeights.CropMatch
                + MatchScoreWeights.LocationMatch
                + MatchScoreWeights.VerifiedFarm
                + MatchScoreWeights.TrustMax);
    }

    [Fact]
    public void PerfectFarm_Scores100()
    {
        Assert.Equal(
            MatchScoreWeights.Max,
            MatchScoreWeights.Compute(locationMatched: true, isVerified: true, trustScore: 100m));
    }

    [Fact]
    public void CropOnlyFarm_KeepsTheHardFilterPoints()
    {
        Assert.Equal(
            MatchScoreWeights.CropMatch,
            MatchScoreWeights.Compute(locationMatched: false, isVerified: false, trustScore: 0m));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(75, 15)]
    [InlineData(100, 20)]
    public void TrustPoints_AreProportional(decimal trustScore, decimal expected)
    {
        Assert.Equal(expected, MatchScoreWeights.TrustPoints(trustScore));
    }

    [Fact]
    public void TrustPoints_AreClamped_ForOutOfRangeScores()
    {
        Assert.Equal(MatchScoreWeights.TrustMax, MatchScoreWeights.TrustPoints(140m));
        Assert.Equal(0m, MatchScoreWeights.TrustPoints(-20m));
    }
}

public class MatchExplanationTests
{
    [Fact]
    public void CropPoints_AreAlwaysEarned_BecauseCropIsAHardFilter()
    {
        var factors = MatchExplanation.Build(new MatchExplanationInputs { CropName = "Tomato" });

        var crop = Single(factors, MatchExplanation.CropCode);
        Assert.Equal(MatchFactorState.Earned, crop.State);
        Assert.Equal(MatchScoreWeights.CropMatch, crop.Points);
        Assert.Equal("Tomato", crop.Detail);
    }

    [Fact]
    public void SnapshotWins_OverLiveProfile()
    {
        // Farm lost verification after being proposed; the explanation must describe
        // the shortlist the factory is looking at, not today's profile.
        var factors = MatchExplanation.Build(new MatchExplanationInputs
        {
            Snapshot = new MatchEligibilitySnapshot
            {
                Governorate = "Qalyubia",
                IsVerified = true,
                LocationMatched = true,
                RiskScore = 80m
            },
            IsVerified = false,
            TrustScore = 10m
        });

        Assert.Equal(MatchFactorState.Earned, Single(factors, MatchExplanation.VerifiedCode).State);
        Assert.Equal("80/100", Single(factors, MatchExplanation.TrustCode).Detail);
    }

    [Fact]
    public void MissedFactors_ReportZeroAgainstTheirMax()
    {
        var factors = MatchExplanation.Build(new MatchExplanationInputs
        {
            Snapshot = new MatchEligibilitySnapshot { LocationMatched = false, IsVerified = false }
        });

        var location = Single(factors, MatchExplanation.LocationOutsideCode);
        Assert.Equal(MatchFactorState.Missed, location.State);
        Assert.Equal(0m, location.Points);
        Assert.Equal(MatchScoreWeights.LocationMatch, location.MaxPoints);

        var verified = Single(factors, MatchExplanation.NotVerifiedCode);
        Assert.Equal(MatchFactorState.Missed, verified.State);
        Assert.Equal(0m, verified.Points);
    }

    [Fact]
    public void LowTrust_IsFlaggedAsCaution_NotEarned()
    {
        var low = MatchExplanation.Build(new MatchExplanationInputs { TrustScore = 30m });
        var ok = MatchExplanation.Build(new MatchExplanationInputs { TrustScore = 65m });

        Assert.Equal(MatchFactorState.Caution, Single(low, MatchExplanation.TrustCode).State);
        Assert.Equal(MatchFactorState.Earned, Single(ok, MatchExplanation.TrustCode).State);
    }

    [Fact]
    public void TrustPoints_MatchTheScoreWeights()
    {
        var factors = MatchExplanation.Build(new MatchExplanationInputs { TrustScore = 75m });

        Assert.Equal(15m, Single(factors, MatchExplanation.TrustCode).Points);
    }

    [Fact]
    public void NoTrustScore_OmitsTheTrustLine_RatherThanShowingZero()
    {
        var factors = MatchExplanation.Build(new MatchExplanationInputs());

        Assert.DoesNotContain(factors, f => f.Code == MatchExplanation.TrustCode);
    }

    [Fact]
    public void Distance_IsShownWhenKnown_FallbackNoteOtherwise()
    {
        var withDistance = MatchExplanation.Build(new MatchExplanationInputs { DistanceKm = 42.4 });
        Assert.Equal("42", Single(withDistance, MatchExplanation.DistanceCode).Detail);

        var fallback = MatchExplanation.Build(new MatchExplanationInputs
        {
            UsedGovernorateFallback = true
        });
        Assert.Equal(
            MatchFactorState.Info,
            Single(fallback, MatchExplanation.GovernorateFallbackCode).State);
        Assert.DoesNotContain(fallback, f => f.Code == MatchExplanation.DistanceCode);
    }

    [Fact]
    public void GeographicExpansion_IsDisclosed()
    {
        var factors = MatchExplanation.Build(new MatchExplanationInputs
        {
            FarmGovernorate = "Sharqia",
            IsGeographicExpansion = true
        });

        Assert.Equal("Sharqia", Single(factors, MatchExplanation.ExpansionCode).Detail);
        // An expanded farm is by definition outside the preferred scope.
        Assert.Contains(factors, f => f.Code == MatchExplanation.LocationOutsideCode);
    }

    [Fact]
    public void ShortCapacity_IsCaution_NotSilentlyHidden()
    {
        var factors = MatchExplanation.Build(new MatchExplanationInputs
        {
            Snapshot = new MatchEligibilitySnapshot { AvailableQuantityTons = 40m },
            RequestQuantityTons = 100m
        });

        var capacity = Single(factors, MatchExplanation.CapacityShortCode);
        Assert.Equal(MatchFactorState.Caution, capacity.State);
        Assert.Equal("40/100", capacity.Detail);
    }

    [Fact]
    public void SufficientCapacity_IsInformational()
    {
        var factors = MatchExplanation.Build(new MatchExplanationInputs
        {
            Snapshot = new MatchEligibilitySnapshot { AvailableQuantityTons = 150m },
            RequestQuantityTons = 100m
        });

        Assert.Equal(MatchFactorState.Info, Single(factors, MatchExplanation.CapacityCode).State);
    }

    [Fact]
    public void PriceFloorAboveOffer_IsCaution()
    {
        var above = MatchExplanation.Build(new MatchExplanationInputs
        {
            Snapshot = new MatchEligibilitySnapshot { MinPricePerTon = 9500m },
            RequestPricePerTon = 9000m
        });
        Assert.Equal(
            MatchFactorState.Caution,
            Single(above, MatchExplanation.PriceFloorAboveCode).State);

        var within = MatchExplanation.Build(new MatchExplanationInputs
        {
            Snapshot = new MatchEligibilitySnapshot { MinPricePerTon = 8500m },
            RequestPricePerTon = 9000m
        });
        Assert.Equal(
            MatchFactorState.Info,
            Single(within, MatchExplanation.PriceFloorCode).State);
    }

    [Fact]
    public void LegacyRowWithoutSnapshot_StillExplainsTheScore()
    {
        var factors = MatchExplanation.Build(new MatchExplanationInputs
        {
            CropName = "Wheat",
            FarmGovernorate = "Giza",
            IsVerified = true,
            TrustScore = 70m
        });

        Assert.Contains(factors, f => f.Code == MatchExplanation.CropCode);
        Assert.Contains(factors, f => f.Code == MatchExplanation.LocationCode);
        Assert.Contains(factors, f => f.Code == MatchExplanation.VerifiedCode);
        Assert.Contains(factors, f => f.Code == MatchExplanation.TrustCode);
    }

    [Fact]
    public void EarnedPoints_ReconcileWithTheRankingScore()
    {
        var snapshot = new MatchEligibilitySnapshot
        {
            LocationMatched = true,
            IsVerified = true,
            RiskScore = 60m
        };
        var factors = MatchExplanation.Build(new MatchExplanationInputs { Snapshot = snapshot });

        var explained = factors.Where(f => f.Points is not null).Sum(f => f.Points!.Value);
        var ranked = MatchScoreWeights.Compute(
            locationMatched: true,
            isVerified: true,
            trustScore: 60m);

        Assert.Equal(ranked, explained);
    }

    [Fact]
    public void SnapshotRoundTrip_KeepsTheNewExplanationFields()
    {
        var json = new MatchEligibilitySnapshot
        {
            LocationMatched = true,
            MatchScore = 92m
        }.ToJson();

        var parsed = MatchEligibilitySnapshot.TryParse(json);

        Assert.NotNull(parsed);
        Assert.True(parsed!.LocationMatched);
        Assert.Equal(92m, parsed.MatchScore);
    }

    [Fact]
    public void LegacySnapshotJson_ParsesWithNullExplanationFields()
    {
        const string legacy = """{"governorate":"Giza","isVerified":true,"cropTypeIds":[]}""";

        var parsed = MatchEligibilitySnapshot.TryParse(legacy);

        Assert.NotNull(parsed);
        Assert.Null(parsed!.LocationMatched);
        Assert.Null(parsed.MatchScore);
    }

    private static MatchFactor Single(IReadOnlyList<MatchFactor> factors, string code) =>
        Assert.Single(factors, f => f.Code == code);
}
