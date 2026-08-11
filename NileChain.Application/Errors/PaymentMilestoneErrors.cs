using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class PaymentMilestoneErrors
{
    public static readonly Error NotFound =
        new("PaymentMilestone.NotFound", "Payment milestone schedule was not found for this contract.");

    public static readonly Error MilestoneNotFound =
        new("PaymentMilestone.MilestoneNotFound", "Payment milestone was not found.");

    public static readonly Error ContractNotFound =
        new("PaymentMilestone.ContractNotFound", "Contract was not found.");

    public static readonly Error ContractNotSigned =
        new("PaymentMilestone.ContractNotSigned", "Payment milestones are only available on signed contracts.");

    public static readonly Error Forbidden =
        new("PaymentMilestone.Forbidden", "You are not allowed to perform this payment status action.");

    public static readonly Error InvalidTransition =
        new("PaymentMilestone.InvalidTransition", "That payment status transition is not allowed.");

    public static readonly Error Voided =
        new("PaymentMilestone.Voided", "This payment milestone schedule has been voided and can no longer be updated.");

    public static readonly Error Conflict =
        new("PaymentMilestone.Conflict", "Payment milestone status changed concurrently — refresh and try again.");

    public static readonly Error FrozenByDispute =
        new(
            "PaymentMilestone.FrozenByDispute",
            "Payment status cannot change while a dispute on this contract is open or under review.");

    public static readonly Error ContractTotalUnavailable =
        new(
            "PaymentMilestone.ContractTotalUnavailable",
            "Cannot build payment milestones because QuantityTons × PricePerTon is unavailable.");
}
