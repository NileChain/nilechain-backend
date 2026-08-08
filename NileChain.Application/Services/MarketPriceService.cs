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
}
