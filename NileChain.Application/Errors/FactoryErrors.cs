using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class FactoryErrors
{
    public static readonly Error FactoryNotFound = new("Factory.NotFound", "Factory not found.");
    public static readonly Error UnauthorizedAccess = new("Factory.UnauthorizedAccess", "You do not have access to this factory resource.");
    public static readonly Error SupplyRequestNotFound = new("Factory.SupplyRequestNotFound", "Supply request not found.");
    public static readonly Error CropTypeNotFound = new("Factory.CropTypeNotFound", "Crop type not found.");
    public static readonly Error MatchNotFound = new("Factory.MatchNotFound", "Farm match not found.");
    public static readonly Error ContractNotFound = new("Factory.ContractNotFound", "Contract not found.");
    public static readonly Error ConversationNotFound = new("Factory.ConversationNotFound", "Conversation not found.");
    public static readonly Error NotificationNotFound = new("Factory.NotificationNotFound", "Notification not found.");
    public static readonly Error InvalidAction = new("Factory.InvalidAction", "Invalid action.");
}
