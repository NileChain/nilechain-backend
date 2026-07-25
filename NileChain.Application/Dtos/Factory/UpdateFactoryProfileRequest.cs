namespace NileChain.Application.Dtos.Factory;

public class UpdateFactoryProfileRequest
{
    public string Name { get; set; } = default!;
    public string? Location { get; set; }
    public string? Governorate { get; set; }
    public string? IndustryType { get; set; }
}
