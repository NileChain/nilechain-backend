using NileChain.Domain.Entities;

namespace NileChain.Domain.Common;

/// <summary>
/// Effective commercial terms for a match: farm counter-offer when present, else supply request.
/// </summary>
public static class MatchCommercialTerms
{
    public static decimal QuantityTons(FarmMatch match) =>
        match.CounterQuantityTons is > 0
            ? match.CounterQuantityTons.Value
            : match.SupplyRequest?.QuantityTons ?? 0m;

    public static decimal? PricePerTon(FarmMatch match) =>
        match.CounterPricePerTon is > 0
            ? match.CounterPricePerTon
            : match.SupplyRequest?.PricePerTon;

    public static DateTime? DeliveryDate(FarmMatch match) =>
        match.CounterDeliveryDate ?? match.SupplyRequest?.DeliveryDate;

    public static bool HasCounter(FarmMatch match) =>
        match.CounteredAt is not null
        || match.CounterQuantityTons is not null
        || match.CounterPricePerTon is not null
        || match.CounterDeliveryDate is not null;
}
