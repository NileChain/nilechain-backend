using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class FarmErrors
{
    public static readonly Error FarmNotFound = new("Farm.NotFound", "Farm not found.");
    public static readonly Error CropTypeNotFound = new("Farm.CropTypeNotFound", "Crop type not found.");
    public static readonly Error CropAlreadyAdded = new("Farm.CropAlreadyAdded", "This crop is already added to the farm.");
    public static readonly Error DocumentNotFound = new("Farm.DocumentNotFound", "Document not found.");
    public static readonly Error UnauthorizedAccess = new("Farm.UnauthorizedAccess", "You do not have access to this farm resource.");
    public static readonly Error MatchNotFound = new("Farm.MatchNotFound", "Match not found.");
    public static readonly Error MatchNotProposed = new(
        "Farm.MatchNotProposed",
        "Only proposed matches can be responded to, and contracts can only be created or signed while the match is Proposed.");
    public static readonly Error PartyInactive = new(
        "Farm.PartyInactive",
        "Cannot proceed because the farm or factory account is inactive.");
    public static readonly Error GovernorateMismatch = new(
        "Farm.GovernorateMismatch",
        "The farm governorate no longer matches the Exact-scope snapshot captured at matching time.");
    public static readonly Error InvalidAction = new("Farm.InvalidAction", "Action must be 'reject'. Accept the offer from the Contract Details page.");
    public static readonly Error ContractNotFound = new("Farm.ContractNotFound", "Contract not found.");
    public static readonly Error ContractNotPending = new("Farm.ContractNotPending", "Only contracts awaiting farm signature can be approved or rejected.");
    public static readonly Error ContractAlreadySignedByFarm = new("Farm.ContractAlreadySignedByFarm", "This contract has already been signed by the farm.");
    public static readonly Error ConversationNotFound = new("Farm.ConversationNotFound", "Conversation not found.");
    public static readonly Error CannotSendMessage = new("Farm.CannotSendMessage", "Cannot send message to this conversation.");
    public static readonly Error NotificationNotFound = new("Farm.NotificationNotFound", "Notification not found.");
    public static readonly Error DocumentInvalid = new("Farm.DocumentInvalid", "The uploaded document is not allowed.");
    public static readonly Error ConcurrencyConflict = new(
        "Farm.ConcurrencyConflict",
        "The contract was modified by another request. Refresh and try again.");
}
