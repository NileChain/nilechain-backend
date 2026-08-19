using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

public static class KybRequirements
{
    public static readonly KybKind[] RequiredForFarmVerifyWarning =
    [
        KybKind.CommercialRegister,
        KybKind.TaxCard,
        KybKind.NationalId
    ];

    public static readonly KybKind[] RequiredForFactoryVerify =
    [
        KybKind.CommercialRegister,
        KybKind.TaxCard,
        KybKind.NationalId
    ];

    public static IReadOnlyList<KybKind> MissingRequiredKinds(
        IEnumerable<KybKind> uploaded,
        IReadOnlyList<KybKind>? required = null)
    {
        var set = uploaded.ToHashSet();
        var kinds = required ?? RequiredForFarmVerifyWarning;
        return kinds.Where(k => !set.Contains(k)).ToList();
    }
}
