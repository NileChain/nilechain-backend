using NileChain.Application.Common;
using NileChain.Application.Dtos.Market;

namespace NileChain.Application.Interfaces;

public interface IMarketPriceService
{
    Task<Result<List<MarketPriceDto>>> GetPricesAsync(string? cropName, string? governorate);
    Task<Result<List<MarketPriceSeriesDto>>> GetSeriesAsync();
    Task<Result<FairPriceHintDto>> GetFairPriceHintAsync(
        string cropName,
        decimal requestedPricePerTon,
        string? governorate);
}
