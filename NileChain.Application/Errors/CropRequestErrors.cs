using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class CropRequestErrors
{
    public static readonly Error NotFound = new("CropRequest.NotFound", "Crop request not found.");
    public static readonly Error NameRequired = new("CropRequest.NameRequired", "Crop name is required.");
    public static readonly Error CropTypeAlreadyExists = new(
        "CropRequest.CropTypeAlreadyExists",
        "A crop type with this name already exists.");
    public static readonly Error PendingRequestAlreadyExists = new(
        "CropRequest.PendingRequestAlreadyExists",
        "A pending crop request with this name already exists.");
    public static readonly Error NotPending = new(
        "CropRequest.NotPending",
        "Only pending crop requests can be reviewed.");
}
