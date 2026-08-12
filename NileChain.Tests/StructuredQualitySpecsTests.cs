using NileChain.Application.Common;
using NileChain.Application.Dtos.Factory;

namespace NileChain.Tests;

public class StructuredQualitySpecsTests
{
    [Fact]
    public void Pack_And_Parse_RoundTrip()
    {
        var packed = StructuredQualitySpecs.Pack(
            "High protein",
            new StructuredQualityInput
            {
                MoistureMaxPercent = 14.5m,
                ImpuritiesMaxPercent = 2m,
                Grade = "A",
                LabRequired = true
            },
            ["Giza", "Cairo"],
            "Nearby",
            Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.Contains("MoistureMax:14.5", packed);
        Assert.Contains("Gov:Giza,Cairo", packed);
        Assert.Contains("GeoScope:Nearby", packed);

        var parsed = StructuredQualitySpecs.Parse(packed);
        Assert.Equal(14.5m, parsed.MoistureMaxPercent);
        Assert.Equal(2m, parsed.ImpuritiesMaxPercent);
        Assert.Equal("A", parsed.Grade);
        Assert.True(parsed.LabRequired);
        Assert.Equal("Nearby", parsed.GeographicScope);
        Assert.Equal(2, parsed.PreferredGovernorates.Count);
        Assert.Equal("High protein", parsed.Notes);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), parsed.PreferredFarmId);
    }

    [Fact]
    public void Parse_Legacy_FreeText_Preserves_Geo()
    {
        var parsed = StructuredQualitySpecs.Parse("Grade A wheat | Gov:Giza | GeoScope:Exact");
        Assert.Equal("Exact", parsed.GeographicScope);
        Assert.Contains("Giza", parsed.PreferredGovernorates);
        Assert.Equal("Grade A wheat", parsed.Notes);
    }
}
