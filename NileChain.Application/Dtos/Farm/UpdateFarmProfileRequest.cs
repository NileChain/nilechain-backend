using NileChain.Domain.Enums;

namespace NileChain.Application.Dtos.Farm;

public class UpdateFarmProfileRequest
{
    public string Name { get; set; } = default!;
    public string? Location { get; set; }
    public string? Governorate { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public decimal? SizeInFeddans { get; set; }
    public SoilType? SoilType { get; set; }
}
