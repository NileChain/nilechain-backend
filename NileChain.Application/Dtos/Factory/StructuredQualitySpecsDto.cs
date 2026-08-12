namespace NileChain.Application.Dtos.Factory;

public class StructuredQualityInput
{
    public decimal? MoistureMaxPercent { get; set; }
    public decimal? ImpuritiesMaxPercent { get; set; }
    public string? Grade { get; set; }
    public bool? LabRequired { get; set; }
    public string? Notes { get; set; }
}

public class StructuredQualitySpecsDto
{
    public string? Raw { get; set; }
    public decimal? MoistureMaxPercent { get; set; }
    public decimal? ImpuritiesMaxPercent { get; set; }
    public string? Grade { get; set; }
    public bool? LabRequired { get; set; }
    public string? Notes { get; set; }
    public string? GeographicScope { get; set; }
    public List<string> PreferredGovernorates { get; set; } = new();
    public Guid? PreferredFarmId { get; set; }
}
