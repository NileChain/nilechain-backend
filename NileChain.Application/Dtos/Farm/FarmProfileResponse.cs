namespace NileChain.Application.Dtos.Farm;

public class FarmProfileResponse
{
    public Guid FarmId { get; set; }
    public string Name { get; set; } = default!;
    public string? Location { get; set; }
    public string? Governorate { get; set; }
    public decimal? SizeInFeddans { get; set; }
    public string? SoilType { get; set; }
    public string? Phone { get; set; }
    public bool IsVerified { get; set; }
    public int CompletionPercent { get; set; }
    public List<CropTypeDto> CropTypes { get; set; } = new();
    public List<FarmDocumentDto> Documents { get; set; } = new();
}
