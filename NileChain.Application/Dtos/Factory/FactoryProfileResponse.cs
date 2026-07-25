namespace NileChain.Application.Dtos.Factory;

public class FactoryProfileResponse
{
    public Guid FactoryId { get; set; }
    public string Name { get; set; } = default!;
    public string? Location { get; set; }
    public string? Governorate { get; set; }
    public string? IndustryType { get; set; }
    public string? Phone { get; set; }
    public bool IsVerified { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    public int CompletionPercent { get; set; }
}
