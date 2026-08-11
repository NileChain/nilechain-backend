namespace NileChain.Application.Dtos.Crop;

public class CreateCropRequestDto
{
    public string Name { get; set; } = default!;
    public string? Category { get; set; }
    public string? Description { get; set; }
}
