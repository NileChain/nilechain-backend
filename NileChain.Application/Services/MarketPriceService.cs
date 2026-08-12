using NileChain.Application.Common;
using NileChain.Application.Dtos.Market;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class MarketPriceService : IMarketPriceService
{
    private readonly IRepository<MarketPrice> _marketPriceRepository;
    private readonly IRepository<CropType> _cropTypeRepository;

    public MarketPriceService(
        IRepository<MarketPrice> marketPriceRepository,
        IRepository<CropType> cropTypeRepository)
    {
        _marketPriceRepository = marketPriceRepository;
        _cropTypeRepository = cropTypeRepository;
    }

    public async Task<Result<List<MarketPriceDto>>> GetPricesAsync(string? cropName, string? governorate)
    {
        var crops = (await _cropTypeRepository.GetAllAsync()).ToDictionary(c => c.CropTypeId);
        var all = await _marketPriceRepository.GetAllAsync();
        var query = all.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(cropName))
        {
            var cropIds = crops.Values
                .Where(c => string.Equals(c.Name, cropName, StringComparison.OrdinalIgnoreCase))
                .Select(c => c.CropTypeId)
                .ToHashSet();
            query = query.Where(p => cropIds.Contains(p.CropTypeId));
        }

        if (!string.IsNullOrWhiteSpace(governorate))
        {
            query = query.Where(p =>
                string.Equals(p.Governorate, governorate, StringComparison.OrdinalIgnoreCase));
        }

        var dtos = query
            .OrderByDescending(p => p.RecordedAt)
            .Select(p => new MarketPriceDto
            {
                PriceId = p.PriceId,
                CropTypeId = p.CropTypeId,
                CropName = crops.TryGetValue(p.CropTypeId, out var crop) ? crop.Name : string.Empty,
                Governorate = p.Governorate,
                PricePerTon = p.PricePerTon,
                Source = p.Source,
                RecordedAt = p.RecordedAt
            })
            .ToList();

        return Result<List<MarketPriceDto>>.Success(dtos);
    }

    public async Task<Result<List<MarketPriceSeriesDto>>> GetSeriesAsync()
    {
        var crops = (await _cropTypeRepository.GetAllAsync()).ToDictionary(c => c.CropTypeId);
        var all = await _marketPriceRepository.GetAllAsync();
        var series = all
            .GroupBy(p => crops.TryGetValue(p.CropTypeId, out var crop) ? crop.Name : "Unknown")
            .Select(g =>
            {
                var ordered = g.OrderBy(p => p.RecordedAt).ToList();
                return new MarketPriceSeriesDto
                {
                    CropName = g.Key,
                    Labels = ordered.Select(p => p.RecordedAt.ToString("MMM")).ToList(),
                    Prices = ordered.Select(p => p.PricePerTon).ToList()
                };
            })
            .Where(s => s.Prices.Count > 0)
            .OrderBy(s => s.CropName)
            .Take(5)
            .ToList();

        return Result<List<MarketPriceSeriesDto>>.Success(series);
    }

    public async Task<Result<FairPriceHintDto>> GetFairPriceHintAsync(
        string cropName,
        decimal requestedPricePerTon,
        string? governorate)
    {
        var hint = new FairPriceHintDto
        {
            CropName = cropName?.Trim() ?? string.Empty,
            Governorate = string.IsNullOrWhiteSpace(governorate) ? null : governorate.Trim(),
            RequestedPricePerTon = requestedPricePerTon,
            Alignment = "NoData",
            Hint = "No market series for this crop yet."
        };

        if (string.IsNullOrWhiteSpace(cropName) || requestedPricePerTon < 0)
            return Result<FairPriceHintDto>.Success(hint);

        var crops = await _cropTypeRepository.GetAllAsync();
        var crop = crops.FirstOrDefault(c =>
            string.Equals(c.Name, cropName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (crop is null)
            return Result<FairPriceHintDto>.Success(hint);

        var all = await _marketPriceRepository.GetAllAsync();
        var query = all.Where(p => p.CropTypeId == crop.CropTypeId);
        if (!string.IsNullOrWhiteSpace(governorate))
        {
            var govFiltered = query.Where(p =>
                string.Equals(p.Governorate, governorate.Trim(), StringComparison.OrdinalIgnoreCase));
            if (govFiltered.Any())
                query = govFiltered;
        }

        var latest = query.OrderByDescending(p => p.RecordedAt).FirstOrDefault();
        if (latest is null || latest.PricePerTon <= 0)
            return Result<FairPriceHintDto>.Success(hint);

        var delta = (requestedPricePerTon - latest.PricePerTon) / latest.PricePerTon * 100m;
        string alignment;
        string text;
        if (Math.Abs(delta) < 8m)
        {
            alignment = "Aligned";
            text = $"Within 8% of the latest {crop.Name} market print ({latest.PricePerTon:0} EGP/t).";
        }
        else if (delta > 0)
        {
            alignment = "AboveMarket";
            text = $"About {delta:0.#}% above the latest {crop.Name} market print ({latest.PricePerTon:0} EGP/t).";
        }
        else
        {
            alignment = "BelowMarket";
            text = $"About {Math.Abs(delta):0.#}% below the latest {crop.Name} market print ({latest.PricePerTon:0} EGP/t).";
        }

        hint.LatestPricePerTon = latest.PricePerTon;
        hint.LatestRecordedAt = latest.RecordedAt;
        hint.PercentageDelta = Math.Round(delta, 1);
        hint.Alignment = alignment;
        hint.Hint = text;
        return Result<FairPriceHintDto>.Success(hint);
    }
}
