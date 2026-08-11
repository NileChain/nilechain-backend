namespace NileChain.Application.Dtos.Crop;

public class CropRequestDto
{
    public Guid CropRequestId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string Name { get; set; } = default!;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string Status { get; set; } = default!;
    public string? AdminNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public Guid? ApprovedCropTypeId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}
