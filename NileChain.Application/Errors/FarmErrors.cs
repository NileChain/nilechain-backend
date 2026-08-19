using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class FarmErrors
{
    public static readonly Error FarmNotFound = new("Farm.NotFound", "Farm not found.");
    public static readonly Error CropTypeNotFound = new("Farm.CropTypeNotFound", "Crop type not found.");
    public static readonly Error CropAlreadyAdded = new("Farm.CropAlreadyAdded", "This crop is already added to the farm.");
    public static readonly Error CropNotOnFarm = new("Farm.CropNotOnFarm", "This crop is not linked to the farm.");
    public static readonly Error InvalidCropAvailability = new(
        "Farm.InvalidCropAvailability",
        "Available quantity and min price must be non-negative, and AvailableTo must be on or after AvailableFrom.");
    public static readonly Error InvalidCounterOffer = new(
        "Farm.InvalidCounterOffer",
        "Provide at least one counter term (quantity, price, or delivery date). Values must be valid.");
    public static readonly Error MatchNotCounterable = new(
        "Farm.MatchNotCounterable",
        "Only proposed or countered matches can receive a counter-offer.");

    public static readonly Error MatchNotCountered = new(
        "Farm.MatchNotCountered",
        "This match has no pending factory counter-offer.");
    public static readonly Error ImageNotFound = new("Farm.ImageNotFound", "Farm image not found.");
    public static readonly Error ImageInvalid = new("Farm.ImageInvalid", "Only JPG, PNG, or WEBP images are allowed.");
    public static readonly Error CertificationNotFound = new("Farm.CertificationNotFound", "Certification not found.");
    public static readonly Error CertificationAlreadyAdded = new(
        "Farm.CertificationAlreadyAdded",
        "This certification is already linked to the farm.");
    public static readonly Error CertificationNotOnFarm = new(
        "Farm.CertificationNotOnFarm",
        "This certification is not linked to the farm.");
    public static readonly Error CertificationForbidden = new(
        "Farm.CertificationForbidden",
        "Certifications are granted by NileChain admin after document review.");
    public static readonly Error InvalidCertificationDates = new(
        "Farm.InvalidCertificationDates",
        "ExpiresAt must be after IssuedAt when provided.");
    public static readonly Error KybKindRequired = new(
        "Farm.KybKindRequired",
        "Choose a document type (commercial register, tax card, national ID, land lease, or other).");
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
    public static readonly Error EligibilityChanged = new(
        "Match.EligibilityChanged",
        "The farm profile changed materially since this match was proposed. Re-run matching before signing.");
    public static readonly Error InvalidAction = new("Farm.InvalidAction", "Action must be 'reject'. Accept the offer from the Contract Details page.");
    public static readonly Error ContractNotFound = new("Farm.ContractNotFound", "Contract not found.");
    public static readonly Error ContractNotPending = new("Farm.ContractNotPending", "Only contracts awaiting farm signature can be approved or rejected.");
    public static readonly Error CannotUnwindAfterReceive = new(
        "Farm.CannotUnwindAfterReceive",
        "A signed contract can only be cancelled before the factory marks goods received. Open a dispute after receipt.");
    public static readonly Error UnwindBlockedByDispute = new(
        "Farm.UnwindBlockedByDispute",
        "Resolve or reject the open dispute before cancelling this signed contract.");
    public static readonly Error ContractAlreadySignedByFarm = new("Farm.ContractAlreadySignedByFarm", "This contract has already been signed by the farm.");
    public static readonly Error ConversationNotFound = new("Farm.ConversationNotFound", "Conversation not found.");
    public static readonly Error CannotSendMessage = new(
        "Farm.CannotSendMessage",
        "Messaging is available once a match is proposed (until it is excluded or rejected).");

    public static readonly Error NegotiationRoundLimit = new(
        "Farm.NegotiationRoundLimit",
        "This match has reached the maximum number of negotiation rounds.");
    public static readonly Error FactoryProfileUnavailable = new(
        "Farm.FactoryProfileUnavailable",
        "Factory profile is available only after a match with that factory.");
    public static readonly Error NotificationNotFound = new("Farm.NotificationNotFound", "Notification not found.");
    public static readonly Error DocumentInvalid = new("Farm.DocumentInvalid", "The uploaded document is not allowed.");
    public static readonly Error ConcurrencyConflict = new(
        "Farm.ConcurrencyConflict",
        "The contract was modified by another request. Refresh and try again.");
}
