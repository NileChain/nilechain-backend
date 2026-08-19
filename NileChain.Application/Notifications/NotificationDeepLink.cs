using System.Text.RegularExpressions;
using NileChain.Domain.Entities;

namespace NileChain.Application.Notifications;

public static class NotificationRelations
{
    public const string Contract = "Contract";
    public const string Match = "Match";
    public const string Conversation = "Conversation";
    public const string Dispute = "Dispute";
    public const string CropRequest = "CropRequest";
    public const string Wallet = "Wallet";
    public const string Profile = "Profile";
}

public static class NotificationDeepLink
{
    private static readonly Regex ContractMarker = new(
        @"#contract:([0-9a-fA-F-]{36})#",
        RegexOptions.Compiled);

    public static string? Resolve(string portal, Notification notification)
    {
        var type = notification.Type ?? string.Empty;
        var relatedType = notification.RelatedEntityType;
        var relatedId = notification.RelatedEntityId;

        if (relatedId is null && TryParseContractMarker(notification.Message, out var embedded))
        {
            relatedType = NotificationRelations.Contract;
            relatedId = embedded;
        }

        if (string.IsNullOrWhiteSpace(relatedType))
            relatedType = InferRelation(type);

        return Build(portal, type, relatedType, relatedId);
    }

    public static string? Build(
        string portal,
        string type,
        string? relatedType,
        Guid? relatedId)
    {
        var p = NormalizePortal(portal);
        var rel = relatedType ?? string.Empty;
        var t = type ?? string.Empty;

        if (string.Equals(rel, NotificationRelations.Contract, StringComparison.OrdinalIgnoreCase)
            || LooksLikeContract(t))
        {
            return relatedId is Guid id && id != Guid.Empty
                ? $"/{p}/contracts/{id}"
                : $"/{p}/contracts";
        }

        if (string.Equals(rel, NotificationRelations.Conversation, StringComparison.OrdinalIgnoreCase)
            || LooksLikeMessage(t))
        {
            return relatedId is Guid id && id != Guid.Empty
                ? $"/{p}/messages?matchId={id}"
                : $"/{p}/messages";
        }

        if (string.Equals(rel, NotificationRelations.Match, StringComparison.OrdinalIgnoreCase)
            || LooksLikeMatch(t))
        {
            return relatedId is Guid id && id != Guid.Empty
                ? p == "factory"
                    ? $"/factory/matches?matchId={id}"
                    : $"/farm/matches?matchId={id}"
                : $"/{p}/matches";
        }

        if (string.Equals(rel, NotificationRelations.Dispute, StringComparison.OrdinalIgnoreCase)
            || LooksLikeDispute(t))
        {
            return relatedId is Guid id && id != Guid.Empty
                ? $"/{p}/disputes?disputeId={id}"
                : $"/{p}/disputes";
        }

        if (string.Equals(rel, NotificationRelations.CropRequest, StringComparison.OrdinalIgnoreCase)
            || LooksLikeCrop(t))
        {
            return p == "admin" ? "/admin/crop-requests" : $"/{p}/crop-request";
        }

        if (string.Equals(rel, NotificationRelations.Wallet, StringComparison.OrdinalIgnoreCase)
            || LooksLikeWallet(t))
        {
            return relatedId is Guid id && id != Guid.Empty
                ? $"/{p}/contracts/{id}"
                : $"/{p}/wallet";
        }

        if (string.Equals(rel, NotificationRelations.Profile, StringComparison.OrdinalIgnoreCase)
            || LooksLikeCert(t))
        {
            return p == "farm" ? "/farm/profile" : "/factory/profile";
        }

        if (LooksLikeRisk(t))
        {
            return p == "factory"
                ? relatedId is Guid id && id != Guid.Empty
                    ? $"/factory/contracts/{id}"
                    : "/factory/risk-report"
                : relatedId is Guid cid && cid != Guid.Empty
                    ? $"/farm/contracts/{cid}"
                    : "/farm/contracts";
        }

        return $"/{p}/notifications";
    }

    private static string InferRelation(string type)
    {
        if (LooksLikeContract(type) || LooksLikeRisk(type) || LooksLikeFulfillment(type) || LooksLikePayment(type))
            return NotificationRelations.Contract;
        if (LooksLikeMessage(type))
            return NotificationRelations.Conversation;
        if (LooksLikeMatch(type))
            return NotificationRelations.Match;
        if (LooksLikeDispute(type))
            return NotificationRelations.Dispute;
        if (LooksLikeCrop(type))
            return NotificationRelations.CropRequest;
        if (LooksLikeCert(type))
            return NotificationRelations.Profile;
        if (LooksLikeWallet(type))
            return NotificationRelations.Wallet;
        return string.Empty;
    }

    private static bool TryParseContractMarker(string? message, out Guid id)
    {
        id = Guid.Empty;
        if (string.IsNullOrWhiteSpace(message))
            return false;
        var m = ContractMarker.Match(message);
        return m.Success && Guid.TryParse(m.Groups[1].Value, out id);
    }

    private static string NormalizePortal(string portal) =>
        portal.Equals("Factory", StringComparison.OrdinalIgnoreCase) ? "factory"
        : portal.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "admin"
        : "farm";

    private static bool LooksLikeContract(string t) =>
        t.Contains("contract", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeMatch(string t) =>
        t.Contains("match", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeMessage(string t) =>
        t.Contains("message", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeDispute(string t) =>
        t.Contains("dispute", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeCrop(string t) =>
        t.Contains("crop", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeCert(string t) =>
        t.Contains("cert", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeWallet(string t) =>
        t.Contains("wallet", StringComparison.OrdinalIgnoreCase)
        || t.Contains("escrow", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeRisk(string t) =>
        t.Contains("risk", StringComparison.OrdinalIgnoreCase)
        || t.Contains("priceshift", StringComparison.OrdinalIgnoreCase)
        || t.Contains("weather", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeFulfillment(string t) =>
        t.Contains("fulfillment", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikePayment(string t) =>
        t.Contains("payment", StringComparison.OrdinalIgnoreCase);
}
