using NileChain.Domain.Enums;
using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class CropRequest
{
    public Guid CropRequestId { get; set; }
    public Guid RequestedByUserId { get; set; }
    public string Name { get; set; } = default!;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public CropRequestStatus Status { get; set; } = CropRequestStatus.Pending;
    public string? AdminNotes { get; set; }
    public Guid? ReviewedByUserId { get; set; }
    public Guid? ApprovedCropTypeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }

    public ApplicationUser RequestedByUser { get; set; } = default!;
    public ApplicationUser? ReviewedByUser { get; set; }
    public CropType? ApprovedCropType { get; set; }
}
