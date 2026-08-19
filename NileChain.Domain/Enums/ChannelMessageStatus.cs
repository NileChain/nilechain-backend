namespace NileChain.Domain.Enums;

/// <summary>Outbound ops channel log. Never an actual Meta/Twilio send.</summary>
public enum ChannelMessageStatus
{
    Queued = 0,
    Logged = 1,
    Failed = 2
}
