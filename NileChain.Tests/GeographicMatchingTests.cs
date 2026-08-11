using NileChain.AI.Matching;

namespace NileChain.Tests;

public class GeographicMatchingTests
{
    [Theory]
    [InlineData("giza", "Giza")]
    [InlineData("Giza", "Giza")]
    [InlineData("alex", "Alexandria")]
    [InlineData("Luxor", "Luxor")]
    [InlineData("minya", "Minya")]
    public void NormalizeGovernorate_MapsAliases(string raw, string expected)
    {
        Assert.Equal(expected, GeographicMatching.NormalizeGovernorate(raw));
    }

    [Fact]
    public void ParsePreferredGovernorates_FromQualitySpecs()
    {
        var prefs = GeographicMatching.ParsePreferredGovernorates(
            "Grade A | Gov:giza,cairo | GeoScope:Exact",
            "Minya");

        Assert.Contains("Giza", prefs);
        Assert.Contains("Cairo", prefs);
        Assert.DoesNotContain("Minya", prefs);
    }

    [Fact]
    public void ParsePreferredGovernorates_FallsBackToFactory()
    {
        var prefs = GeographicMatching.ParsePreferredGovernorates(
            "Grade A",
            "Giza");

        Assert.Single(prefs);
        Assert.Equal("Giza", prefs[0]);
    }

    [Fact]
    public void ExactScope_ExcludesDistantGovernorates()
    {
        var preferred = new[] { "Giza" };
        var scope = GeographicMatching.Scope.Exact;

        Assert.True(GeographicMatching.IsEligible("Giza", preferred, scope));
        Assert.False(GeographicMatching.IsEligible("Luxor", preferred, scope));
        Assert.False(GeographicMatching.IsEligible("Minya", preferred, scope));
        Assert.False(GeographicMatching.IsEligible(null, preferred, scope));
    }

    [Fact]
    public void NearbyGovernorateFallback_AllowsAdjacentButNotDistant()
    {
        // Deliberate: IsEligible(Nearby) remains the missing-coords fallback set
        // (preferred + adjacency). Primary Nearby filtering is haversine in MatchingPlugin.
        var preferred = new[] { "Giza" };
        var scope = GeographicMatching.Scope.Nearby;

        Assert.True(GeographicMatching.IsEligible("Giza", preferred, scope));
        Assert.True(GeographicMatching.IsEligible("Cairo", preferred, scope)); // adjacent
        Assert.False(GeographicMatching.IsEligible("Luxor", preferred, scope));
    }

    [Fact]
    public void NationwideScope_AllowsAnyGovernorate()
    {
        var preferred = new[] { "Giza" };
        var scope = GeographicMatching.Scope.Nationwide;

        Assert.True(GeographicMatching.IsEligible("Luxor", preferred, scope));
        Assert.True(GeographicMatching.IsEligible("Minya", preferred, scope));
    }

    [Fact]
    public void ParseScope_DefaultsToExactWhenPreferredExist()
    {
        var scope = GeographicMatching.ParseScope("Gov:Giza", hasPreferredGovernorates: true);
        Assert.Equal(GeographicMatching.Scope.Exact, scope);
    }

    [Fact]
    public void ParseScope_ReadsExplicitMarker()
    {
        var scope = GeographicMatching.ParseScope(
            "Gov:Giza | GeoScope:Nationwide",
            hasPreferredGovernorates: true);
        Assert.Equal(GeographicMatching.Scope.Nationwide, scope);
    }

    [Fact]
    public void Acceptance_GizaRequest_DoesNotMatchLuxorUnderExact()
    {
        // Factory request location = Giza → Luxor must not be eligible under Exact.
        var preferred = GeographicMatching.ParsePreferredGovernorates("Gov:Giza | GeoScope:Exact", null);
        var scope = GeographicMatching.ParseScope("Gov:Giza | GeoScope:Exact", preferred.Count > 0);

        Assert.Equal(GeographicMatching.Scope.Exact, scope);
        Assert.True(GeographicMatching.IsEligible("Giza", preferred, scope));
        Assert.False(GeographicMatching.IsEligible("Luxor", preferred, scope));
        Assert.False(GeographicMatching.IsEligible("New Valley", preferred, scope));
        Assert.False(GeographicMatching.IsEligible("South Sinai", preferred, scope));
        Assert.False(GeographicMatching.IsEligible("Cairo", preferred, scope));
        Assert.False(GeographicMatching.IsEligible("Minya", preferred, scope));
        Assert.False(GeographicMatching.IsEligible("Qena", preferred, scope));
    }
}
