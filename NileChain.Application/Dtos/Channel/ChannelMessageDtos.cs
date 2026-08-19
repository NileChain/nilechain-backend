namespace NileChain.Application.Dtos.Channel;

public class ChannelMessageDto
{
    public Guid ChannelMessageId { get; set; }
    public string Channel { get; set; } = "WhatsApp";
    public string ToPhone { get; set; } = string.Empty;
    public Guid? UserId { get; set; }
    public string TemplateKey { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? FailReason { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ChannelMessageListDto
{
    public List<ChannelMessageDto> Items { get; set; } = [];
    public string Disclaimer { get; set; } =
        "WhatsApp stub — messages are logged only. NileChain does not send to Meta or Twilio.";
}
