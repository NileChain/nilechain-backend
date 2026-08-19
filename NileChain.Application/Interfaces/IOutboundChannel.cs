using NileChain.Application.Common;
using NileChain.Application.Dtos.Channel;

namespace NileChain.Application.Interfaces;

public static class ChannelTemplates
{
    public const string WhatsApp = "WhatsApp";
    public const string MatchProposed = "MatchProposed";
    public const string ContractSigned = "ContractSigned";
    public const string Shipped = "Shipped";
    public const string PaymentDue = "PaymentDue";
}

/// <summary>
/// Provider-agnostic outbound enqueue. Implementations must not call Twilio/Meta.
/// Failures must never throw to the caller.
/// </summary>
public interface IOutboundChannel
{
    Task EnqueueWhatsAppAsync(
        Guid? userId,
        string? toPhone,
        string templateKey,
        string body,
        string? relatedEntityType,
        Guid? relatedEntityId);

    Task<Result<ChannelMessageListDto>> ListForAdminAsync(string? status, int take = 100);
}
