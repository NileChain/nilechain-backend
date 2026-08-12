namespace NileChain.Application.Dtos.Farm;

public class ConversationDto
{
    public Guid MatchId { get; set; }
    public Guid? FactoryId { get; set; }
    public string FactoryName { get; set; } = default!;
    public string? CropName { get; set; }
    public string Status { get; set; } = default!;
    public DateTime MatchCreatedAt { get; set; }
    public decimal QuantityTons { get; set; }
    public decimal? PricePerTon { get; set; }
    public DateTime? DeliveryDate { get; set; }
    public Guid? ContractId { get; set; }
    public bool ContractFullySigned { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public class MessageDto
{
    public Guid MessageId { get; set; }
    public Guid MatchId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = default!;
    public string Content { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendMessageRequest
{
    public string Content { get; set; } = default!;
}
