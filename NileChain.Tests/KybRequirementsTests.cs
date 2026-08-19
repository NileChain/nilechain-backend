using NileChain.Domain.Common;
using NileChain.Domain.Enums;

namespace NileChain.Tests;

public class KybRequirementsTests
{
    [Fact]
    public void MissingRequiredKinds_UsesRoleList()
    {
        var farmMissing = KybRequirements.MissingRequiredKinds(
            [KybKind.TaxCard],
            KybRequirements.RequiredForFarmVerifyWarning);

        Assert.Contains(KybKind.CommercialRegister, farmMissing);
        Assert.Contains(KybKind.NationalId, farmMissing);
        Assert.DoesNotContain(KybKind.TaxCard, farmMissing);

        var factoryMissing = KybRequirements.MissingRequiredKinds(
            [KybKind.CommercialRegister, KybKind.TaxCard, KybKind.NationalId],
            KybRequirements.RequiredForFactoryVerify);

        Assert.Empty(factoryMissing);
    }
}
