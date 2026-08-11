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
    public static readonly Error PartyInactive = new(
        "Factory.PartyInactive",
        "Cannot proceed because the farm or factory account is inactive.");
    public static readonly Error GovernorateMismatch = new(
        "Factory.GovernorateMismatch",
        "The farm governorate no longer matches the Exact-scope snapshot captured at matching time.");
    public static readonly Error ContractNotFound = new("Factory.ContractNotFound", "Contract not found.");
    public static readonly Error ContractNotPending = new("Factory.ContractNotPending", "Only contracts awaiting factory signature can be approved or rejected.");
    public static readonly Error ContractAlreadySignedByFactory = new("Factory.ContractAlreadySignedByFactory", "This contract has already been signed by the factory.");
    public static readonly Error ConversationNotFound = new("Factory.ConversationNotFound", "Conversation not found.");
    public static readonly Error NotificationNotFound = new("Factory.NotificationNotFound", "Notification not found.");
    public static readonly Error InvalidAction = new("Factory.InvalidAction", "Invalid action.");
    public static readonly Error ConcurrencyConflict = new(
        "Factory.ConcurrencyConflict",
        "The contract was modified by another request. Refresh and try again.");
    public static readonly Error IdempotencyConflict = new(
        "Factory.IdempotencyConflict",
        "A supply request with this idempotency key already exists with different payload.");
}
