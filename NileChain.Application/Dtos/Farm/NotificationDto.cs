namespace NileChain.Application.Dtos.Farm;

public class FarmNotificationDto
{
    public Guid NotificationId { get; set; }
    public string Title { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string? Type { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Link { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
}
