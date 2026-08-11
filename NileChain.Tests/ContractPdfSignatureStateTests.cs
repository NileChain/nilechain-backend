using NileChain.Application.Services;

namespace NileChain.Tests;

public class ContractPdfSignatureStateTests
{
    [Fact]
    public void GeneratePdf_FactoryOnlySigned_DiffersFromBothSignedAndNeither()
    {
        var pdf = new ContractPdfService();
        var factoryAt = DateTime.UtcNow.AddMinutes(-5);
        var farmAt = DateTime.UtcNow;

        var neither = pdf.GeneratePdf(
            "Agricultural Supply Contract",
            "Sample contract body.",
            "Green Farm",
            "Nile Mill",
            factorySigned: false,
            farmSigned: false);

        var factoryOnly = pdf.GeneratePdf(
            "Agricultural Supply Contract",
            "Sample contract body.",
            "Green Farm",
            "Nile Mill",
            factorySigned: true,
            farmSigned: false,
            factorySignedAt: factoryAt,
            farmSignedAt: null);

        var both = pdf.GeneratePdf(
            "Agricultural Supply Contract",
            "Sample contract body.",
            "Green Farm",
            "Nile Mill",
            factorySigned: true,
            farmSigned: true,
            factorySignedAt: factoryAt,
            farmSignedAt: farmAt);

        Assert.True(neither.Length > 100);
        Assert.True(factoryOnly.Length > 100);
        Assert.True(both.Length > 100);
        Assert.False(neither.SequenceEqual(factoryOnly));
        Assert.False(factoryOnly.SequenceEqual(both));
        Assert.False(neither.SequenceEqual(both));
    }

    [Fact]
    public void GeneratePdf_FarmOnlySigned_DiffersFromFactoryOnly()
    {
        var pdf = new ContractPdfService();
        var at = DateTime.UtcNow;

        var factoryOnly = pdf.GeneratePdf(
            "Agricultural Supply Contract",
            "Sample contract body.",
            "Green Farm",
            "Nile Mill",
            factorySigned: true,
            farmSigned: false,
            factorySignedAt: at,
            farmSignedAt: null);

        var farmOnly = pdf.GeneratePdf(
            "Agricultural Supply Contract",
            "Sample contract body.",
            "Green Farm",
            "Nile Mill",
            factorySigned: false,
            farmSigned: true,
            factorySignedAt: null,
            farmSignedAt: at);

        Assert.False(factoryOnly.SequenceEqual(farmOnly));
    }
}
