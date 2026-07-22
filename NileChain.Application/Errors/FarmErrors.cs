using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class FarmErrors
{
    public static readonly Error FarmNotFound = new("Farm.NotFound", "Farm not found.");
    public static readonly Error CropTypeNotFound = new("Farm.CropTypeNotFound", "Crop type not found.");
    public static readonly Error CropAlreadyAdded = new("Farm.CropAlreadyAdded", "This crop is already added to the farm.");
    public static readonly Error DocumentNotFound = new("Farm.DocumentNotFound", "Document not found.");
    public static readonly Error UnauthorizedAccess = new("Farm.UnauthorizedAccess", "You do not have access to this farm resource.");
}
