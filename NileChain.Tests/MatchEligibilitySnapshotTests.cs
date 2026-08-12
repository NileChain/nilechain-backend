using NileChain.Domain.Matching;

namespace NileChain.Tests;

public class MatchEligibilitySnapshotTests
{
    [Fact]
    public void HasMaterialDrift_DetectsGovernorateChange()
    {
        var snap = new MatchEligibilitySnapshot
        {
            Governorate = "Cairo",
            CropTypeIds = [Guid.Parse("11111111-1111-1111-1111-111111111111")],
            RiskScore = 70,
            IsVerified = true
        };
        var live = new MatchEligibilitySnapshot
        {
            Governorate = "Aswan",
            CropTypeIds = snap.CropTypeIds.ToList(),
            RiskScore = 70,
            IsVerified = true
        };

        Assert.True(MatchEligibilitySnapshot.HasMaterialDrift(snap, live, 50, 10000));
    }

    [Fact]
    public void HasMaterialDrift_IgnoresLegacyMissingFields()
    {
        var snap = new MatchEligibilitySnapshot
        {
            Governorate = "Cairo",
            CropTypeIds = [],
            RiskScore = 70,
            IsVerified = true
        };
        var live = new MatchEligibilitySnapshot
        {
            Governorate = "Cairo",
            CropTypeIds = [Guid.NewGuid()],
            RiskScore = 68,
            IsVerified = true
        };

        Assert.False(MatchEligibilitySnapshot.HasMaterialDrift(snap, live, null, null));
    }

    [Fact]
    public void RoundTrip_Json()
    {
        var snap = new MatchEligibilitySnapshot
        {
            Governorate = "Giza",
            CropTypeIds = [Guid.NewGuid()],
            AvailableQuantityTons = 100,
            MinPricePerTon = 9000,
            RiskScore = 55,
            IsVerified = false
        };

        var parsed = MatchEligibilitySnapshot.TryParse(snap.ToJson());
        Assert.NotNull(parsed);
        Assert.Equal("Giza", parsed!.Governorate);
        Assert.Equal(100m, parsed.AvailableQuantityTons);
    }

    [Fact]
    public void HasMaterialDrift_DetectsDeliveryPointChange()
    {
        var snap = new MatchEligibilitySnapshot
        {
            Governorate = "Qalyubia",
            DeliveryPoint = "FactoryGate",
            FreightPayer = "Farm",
            TransitRisk = "Farm",
            IsVerified = true
        };
        var live = new MatchEligibilitySnapshot
        {
            Governorate = "Qalyubia",
            DeliveryPoint = "FarmGate",
            FreightPayer = "Farm",
            TransitRisk = "Farm",
            IsVerified = true
        };

        Assert.True(MatchEligibilitySnapshot.HasMaterialDrift(snap, live, null, null));
    }

    [Fact]
    public void HasMaterialDrift_LegacySnapshotWithoutDeliveryTerms_DoesNotDrift()
    {
        var snap = new MatchEligibilitySnapshot
        {
            Governorate = "Qalyubia",
            IsVerified = true
        };
        var live = new MatchEligibilitySnapshot
        {
            Governorate = "Qalyubia",
            DeliveryPoint = "FarmGate",
            IsVerified = true
        };

        Assert.False(MatchEligibilitySnapshot.HasMaterialDrift(snap, live, null, null));
    }
}
