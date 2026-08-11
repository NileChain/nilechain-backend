using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class DisputeErrors
{
    public static readonly Error NotFound =
        new("Dispute.NotFound", "Dispute was not found.");

    public static readonly Error ContractNotFound =
        new("Dispute.ContractNotFound", "Contract was not found.");

    public static readonly Error ContractNotSigned =
        new("Dispute.ContractNotSigned", "Disputes can only be opened on fully signed contracts.");

    public static readonly Error Forbidden =
        new("Dispute.Forbidden", "You are not allowed to perform this dispute action.");

    public static readonly Error ActiveExists =
        new("Dispute.ActiveExists", "This contract already has an open or under-review dispute.");

    public static readonly Error InvalidTransition =
        new("Dispute.InvalidTransition", "That dispute status change is not allowed from the current state.");

    public static readonly Error Conflict =
        new("Dispute.Conflict", "The dispute was updated by another action. Refresh and try again.");

    public static readonly Error AdminNoteRequired =
        new("Dispute.AdminNoteRequired", "A short admin note is required when resolving or rejecting a dispute.");

    public static readonly Error OutcomeFavorRequired =
        new(
            "Dispute.OutcomeFavorRequired",
            "Resolved disputes must record an operational outcome favor (farm or factory).");

    public static readonly Error DescriptionRequired =
        new("Dispute.DescriptionRequired", "A description is required to open a dispute.");

    public static readonly Error InvalidType =
        new("Dispute.InvalidType", "Dispute type is invalid.");

    public static readonly Error RegenBlocked =
        new(
            "Dispute.RegenBlocked",
            "Contract text cannot be regenerated while a dispute is open or under review.");

    public static readonly Error EvidenceRequired =
        new("Dispute.EvidenceInvalid", "One or more evidence files are invalid.");
}
