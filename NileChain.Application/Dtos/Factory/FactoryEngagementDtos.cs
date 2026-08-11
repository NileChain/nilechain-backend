namespace NileChain.Application.Dtos.Factory;

public class FactoryNotificationDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Link { get; set; }
}

public class FactoryConversationDto
{
    public Guid MatchId { get; set; }
    public string FarmName { get; set; } = default!;
    public string? CropName { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public class FactoryMatchedFarmDto
{
    public Guid FarmId { get; set; }
    public string FarmName { get; set; } = default!;
    public Guid? RequestId { get; set; }
    public Guid? MatchId { get; set; }
    public decimal? MatchScore { get; set; }
    public decimal? RiskScore { get; set; }
    public string? FarmGovernorate { get; set; }
    /// <summary>Most recent match CreatedAt for this farm (list default: newest first).</summary>
    public DateTime CreatedAt { get; set; }
}

public class FactoryMessageDto
{
    public Guid MessageId { get; set; }
    public Guid MatchId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = default!;
    public string Content { get; set; } = default!;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class SendFactoryMessageRequest
{
    public string Content { get; set; } = default!;
}
