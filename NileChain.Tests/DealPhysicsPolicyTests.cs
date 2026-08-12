using NileChain.AI.Plugins;
using NileChain.Domain.Common;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class DealPhysicsPolicyTests
{
    [Fact]
    public void FactoryGate_DefaultsFreightAndTransitToFarm()
    {
        var terms = DeliveryTermsPolicy.Resolve(null, null, null);
        Assert.Equal(DeliveryPoint.FactoryGate, terms.Point);
        Assert.Equal(DealParty.Farm, terms.FreightPayer);
        Assert.Equal(DealParty.Farm, terms.TransitRisk);
        Assert.Equal(DealParty.Farm, DeliveryTermsPolicy.ReturnFreightBearer(terms.Point));
    }

    [Fact]
    public void FarmGate_DefaultsFreightAndTransitToFactory()
    {
        var terms = DeliveryTermsPolicy.Resolve(DeliveryPoint.FarmGate, null, null);
        Assert.Equal(DealParty.Factory, terms.FreightPayer);
        Assert.Equal(DealParty.Factory, terms.TransitRisk);
        Assert.Equal(DealParty.Factory, DeliveryTermsPolicy.ReturnFreightBearer(terms.Point));
    }

    [Fact]
    public void ContractPrompt_IncludesArabicDeliveryPoint()
    {
        var plugin = new ContractPlugin();
        var prompt = plugin.BuildContractPrompt(
            "مزرعة النيل",
            "مصنع قها",
            "Tomato",
            10,
            7000,
            "01 August 2026",
            "Brix 4.5",
            "rag",
            deliveryPointArabic: "باب المزرعة",
            freightPayerArabic: "المصنع",
            transitRiskArabic: "المصنع");

        Assert.Contains("باب المزرعة", prompt, StringComparison.Ordinal);
        Assert.Contains("أجرة النقل يتحملها: المصنع", prompt, StringComparison.Ordinal);
    }
}
