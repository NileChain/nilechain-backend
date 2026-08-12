using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class FulfillmentErrors
{
    public static readonly Error NotFound = new(
        "Fulfillment.NotFound",
        "Fulfillment record was not found for this contract.");

    public static readonly Error ContractNotSigned = new(
        "Fulfillment.ContractNotSigned",
        "Fulfillment is only available after the contract is fully signed.");

    public static readonly Error InvalidTransition = new(
        "Fulfillment.InvalidTransition",
        "That fulfillment status change is not allowed from the current state.");

    public static readonly Error Forbidden = new(
        "Fulfillment.Forbidden",
        "You are not allowed to perform this fulfillment action.");

    public static readonly Error Voided = new(
        "Fulfillment.Voided",
        "This fulfillment has been voided and can no longer be updated.");

    public static readonly Error Conflict = new(
        "Fulfillment.Conflict",
        "The fulfillment was updated by another action. Refresh and try again.");

    public static readonly Error FrozenByDispute = new(
        "Fulfillment.FrozenByDispute",
        "Fulfillment status cannot change while a dispute on this contract is open or under review.");

    public static readonly Error InvalidQualityCheck = new(
        "Fulfillment.InvalidQualityCheck",
        "Quality check values are invalid (accepted quantity and discount percent).");

    public static readonly Error ContractNotFound = new(
        "Fulfillment.ContractNotFound",
        "Contract not found.");

    public static readonly Error WeighbridgeRequired = new(
        "Fulfillment.WeighbridgeRequired",
        "Weighed quantity in tons is required when marking the delivery received.");

    public static readonly Error AcceptedExceedsWeighed = new(
        "Fulfillment.AcceptedExceedsWeighed",
        "Accepted QC quantity cannot exceed the weighbridge tons recorded at receive.");

    public static readonly Error GateRejectNotesRequired = new(
        "Fulfillment.GateRejectNotesRequired",
        "Notes are required when the gate-reject reason is Other.");

    public static readonly Error InvalidGateReject = new(
        "Fulfillment.InvalidGateReject",
        "A valid gate-reject reason is required.");
}
