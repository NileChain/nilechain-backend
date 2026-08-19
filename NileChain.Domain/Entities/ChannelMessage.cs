using NileChain.Domain.Enums;

namespace NileChain.Domain.Entities;

/// <summary>
/// Provider-agnostic outbound message log (WhatsApp stub). Rows are written locally;
/// nothing is POSTed to Twilio or Meta.
/// </summary>
public class ChannelMessage
{
    public Guid ChannelMessageId { get; set; }
    public string Channel { get; set; } = "WhatsApp";
    public string ToPhone { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public ChannelMessageStatus Status { get; set; } = ChannelMessageStatus.Logged;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? FailReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
