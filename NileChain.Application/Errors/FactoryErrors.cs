using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class FactoryErrors
{
    public static readonly Error FactoryNotFound = new("Factory.NotFound", "Factory not found.");
    public static readonly Error UnauthorizedAccess = new("Factory.UnauthorizedAccess", "You do not have access to this factory resource.");
    public static readonly Error SupplyRequestNotFound = new("Factory.SupplyRequestNotFound", "Supply request not found.");
}
