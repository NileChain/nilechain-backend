using NileChain.Domain.Common;
using NileChain.Domain.Entities;

namespace NileChain.Application.Contracts;

/// <summary>Commercial deal total used for wallet hold at full contract signature.</summary>
public static class ContractDealFunding
{
    public static bool TryGetDealTotalEgp(FarmMatch? match, out decimal dealTotalEgp)
    {
        dealTotalEgp = 0m;
        if (match is null)
            return false;

        var qty = MatchCommercialTerms.QuantityTons(match);
        var price = MatchCommercialTerms.PricePerTon(match);
        if (qty <= 0 || price is null or <= 0)
            return false;

        dealTotalEgp = decimal.Round(qty * price.Value, 2, MidpointRounding.AwayFromZero);
        return dealTotalEgp > 0;
    }
}
