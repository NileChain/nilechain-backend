using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class FactoryErrors
{
    public static readonly Error FactoryNotFound = new("Factory.NotFound", "Factory not found.");
    public static readonly Error UnauthorizedAccess = new("Factory.UnauthorizedAccess", "You do not have access to this factory resource.");
    public static readonly Error SupplyRequestNotFound = new("Factory.SupplyRequestNotFound", "Supply request not found.");
    public static readonly Error CropTypeNotFound = new("Factory.CropTypeNotFound", "Crop type not found.");
    public static readonly Error MatchNotFound = new("Factory.MatchNotFound", "Farm match not found.");
    public static readonly Error MatchNotProposed = new(
        "Factory.MatchNotProposed",
        "Contracts can only be created, updated, or signed while the match is Proposed.");
    public static readonly Error MatchCannotExclude = new(
        "Factory.MatchCannotExclude",
        "Only proposed matches can be excluded.");
    public static readonly Error MatchNotCountered = new(
        "Factory.MatchNotCountered",
        "This match has no pending farm counter-offer.");
    public static readonly Error PartyInactive = new(
        "Factory.PartyInactive",
        "Cannot proceed because the farm or factory account is inactive.");
    public static readonly Error GovernorateMismatch = new(
        "Factory.GovernorateMismatch",
        "The farm governorate no longer matches the Exact-scope snapshot captured at matching time.");
    public static readonly Error EligibilityChanged = new(
        "Match.EligibilityChanged",
        "The farm profile changed materially since this match was proposed. Re-run matching or ask the factory to acknowledge before signing.");
    public static readonly Error ContractNotFound = new("Factory.ContractNotFound", "Contract not found.");
    public static readonly Error ContractNotPending = new("Factory.ContractNotPending", "Only contracts awaiting factory signature can be approved or rejected.");
    public static readonly Error CannotUnwindAfterReceive = new(
        "Factory.CannotUnwindAfterReceive",
        "A signed contract can only be cancelled before the factory marks goods received. Open a dispute after receipt.");
    public static readonly Error UnwindBlockedByDispute = new(
        "Factory.UnwindBlockedByDispute",
        "Resolve or reject the open dispute before cancelling this signed contract.");
    public static readonly Error ContractAlreadySignedByFactory = new("Factory.ContractAlreadySignedByFactory", "This contract has already been signed by the factory.");
    public static readonly Error ConversationNotFound = new("Factory.ConversationNotFound", "Conversation not found.");
    public static readonly Error CannotSendMessage = new(
        "Factory.CannotSendMessage",
        "Messaging is available only after both parties have signed the contract.");
    public static readonly Error NotificationNotFound = new("Factory.NotificationNotFound", "Notification not found.");
    public static readonly Error InvalidAction = new("Factory.InvalidAction", "Invalid action.");
    public static readonly Error ConcurrencyConflict = new(
        "Factory.ConcurrencyConflict",
        "The contract was modified by another request. Refresh and try again.");
    public static readonly Error IdempotencyConflict = new(
        "Factory.IdempotencyConflict",
        "A supply request with this idempotency key already exists with different payload.");
    public static readonly Error SupplyRequestCannotCancel = new(
        "Factory.SupplyRequestCannotCancel",
        "Only pending or matched supply requests without a signed contract can be cancelled.");
    public static readonly Error SupplyRequestCannotUpdate = new(
        "Factory.SupplyRequestCannotUpdate",
        "Delivery terms can only be changed on pending or matched requests without a signed contract.");
    public static readonly Error DeliveryTermsRequired = new(
        "Factory.DeliveryTermsRequired",
        "Delivery point is required.");
    public static readonly Error InvalidDeliveryTerms = new(
        "Factory.InvalidDeliveryTerms",
        "Delivery point must be FarmGate or FactoryGate; freight and transit risk must be Farm or Factory.");
    public static readonly Error FarmNotFound = new(
        "Factory.FarmNotFound",
        "Farm not found.");
}
