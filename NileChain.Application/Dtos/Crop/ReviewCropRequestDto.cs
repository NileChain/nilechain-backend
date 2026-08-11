namespace NileChain.Application.Dtos.Crop;

/// <summary>
/// Shared body for approve/reject. Name/Category/Description are optional overrides used mainly on approve.
/// </summary>
public class ReviewCropRequestDto
{
    public string? Name { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string? AdminNotes { get; set; }
}
