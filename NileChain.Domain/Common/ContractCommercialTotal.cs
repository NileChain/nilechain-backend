using NileChain.Domain.Entities;

namespace NileChain.Domain.Common;

/// <summary>
/// Contract commercial total for milestone Amount = qty × price on the supply request.
/// Not stored on Contract itself.
/// </summary>
public static class ContractCommercialTotal
{
    public static bool TryCompute(SupplyRequest? supplyRequest, out decimal total, out string? unavailableReason)
    {
        total = 0m;
        unavailableReason = null;

        if (supplyRequest is null)
        {
            unavailableReason = "Supply request was not found for this contract.";
            return false;
        }

        if (supplyRequest.QuantityTons <= 0)
        {
            unavailableReason = "QuantityTons must be greater than zero to build a payment milestone schedule.";
            return false;
        }

        if (supplyRequest.PricePerTon is null || supplyRequest.PricePerTon <= 0)
        {
            unavailableReason =
                "PricePerTon is missing or invalid — cannot compute milestone Amount (status schedule not created).";
            return false;
        }

        total = decimal.Round(
            supplyRequest.QuantityTons * supplyRequest.PricePerTon.Value,
            2,
            MidpointRounding.AwayFromZero);
        return true;
    }
}
