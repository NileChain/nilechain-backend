using NileChain.Domain.Enums;

namespace NileChain.Domain.Common;

/// <summary>
/// Egyptian truck defaults: factory-gate delivery, farm pays the truck and bears spoilage in transit.
/// Farm-gate flips freight and transit onto the factory (they picked up).
/// </summary>
public static class DeliveryTermsPolicy
{
    public const DeliveryPoint DefaultPoint = DeliveryPoint.FactoryGate;

    public static (DeliveryPoint Point, DealParty FreightPayer, DealParty TransitRisk) Resolve(
        DeliveryPoint? point,
        DealParty? freightPayer,
        DealParty? transitRisk)
    {
        var p = point ?? DefaultPoint;
        var freight = freightPayer ?? DefaultFreight(p);
        var transit = transitRisk ?? DefaultTransit(p);
        return (p, freight, transit);
    }

    public static DealParty DefaultFreight(DeliveryPoint point) =>
        point == DeliveryPoint.FarmGate ? DealParty.Factory : DealParty.Farm;

    public static DealParty DefaultTransit(DeliveryPoint point) =>
        point == DeliveryPoint.FarmGate ? DealParty.Factory : DealParty.Farm;

    /// <summary>
    /// Who pays to send a rejected load back: the party that owned the truck to the factory gate.
    /// FarmGate = factory drove it in, factory pays return. FactoryGate = farm's truck, farm pays return.
    /// </summary>
    public static DealParty ReturnFreightBearer(DeliveryPoint point) =>
        point == DeliveryPoint.FarmGate ? DealParty.Factory : DealParty.Farm;

    public static string ArabicPoint(DeliveryPoint point) =>
        point == DeliveryPoint.FarmGate ? "باب المزرعة" : "باب المصنع";

    public static string ArabicParty(DealParty party) =>
        party == DealParty.Farm ? "المزرعة" : "المصنع";

    public static bool TryParsePoint(string? raw, out DeliveryPoint point)
    {
        point = DefaultPoint;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        return Enum.TryParse(raw.Trim(), ignoreCase: true, out point);
    }

    public static bool TryParseParty(string? raw, out DealParty party)
    {
        party = DealParty.Farm;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        return Enum.TryParse(raw.Trim(), ignoreCase: true, out party);
    }
}
