namespace NileChain.Application.Dtos.Farm;

/// <summary>
/// Light factory profile visible to a farm only after a shared Match.
/// </summary>
public sealed class FactoryPublicProfileDto
{
    public Guid FactoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Governorate { get; set; }
    public string? Location { get; set; }
    public string? IndustryType { get; set; }
    public bool IsVerified { get; set; }
    public decimal AverageRating { get; set; }
    public int RatingCount { get; set; }
    /// <summary>Most recent non-rejected match with the requesting farm, if any.</summary>
    public Guid? ActiveMatchId { get; set; }
    public Guid? ActiveContractId { get; set; }
    public bool ContractFullySigned { get; set; }
    public bool CanMessage { get; set; }
}
