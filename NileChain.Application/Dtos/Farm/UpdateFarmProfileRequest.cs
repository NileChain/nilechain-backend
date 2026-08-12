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
    public string? Description { get; set; }
    public string? BankName { get; set; }
    public string? AccountHolderName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? Iban { get; set; }
}
