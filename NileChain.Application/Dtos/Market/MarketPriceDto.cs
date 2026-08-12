namespace NileChain.Application.Dtos.Market;

public class MarketPriceDto
{
    public Guid PriceId { get; set; }
    public Guid CropTypeId { get; set; }
    public string CropName { get; set; } = string.Empty;
    public string? Governorate { get; set; }
    public decimal PricePerTon { get; set; }
    public string? Source { get; set; }
    public DateTime RecordedAt { get; set; }
}

public class MarketPriceSeriesDto
{
    public string CropName { get; set; } = string.Empty;
    public List<string> Labels { get; set; } = new();
    public List<decimal> Prices { get; set; } = new();
}

public class FairPriceHintDto
{
    public string CropName { get; set; } = string.Empty;
    public string? Governorate { get; set; }
    public decimal RequestedPricePerTon { get; set; }
    public decimal? LatestPricePerTon { get; set; }
    public DateTime? LatestRecordedAt { get; set; }
    public decimal? PercentageDelta { get; set; }
    public string Alignment { get; set; } = "NoData";
    public string Hint { get; set; } = string.Empty;
}
