using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.API.Extensions;
using NileChain.Application.Interfaces;

namespace NileChain.API.Controllers;

[Route("api/market-prices")]
[ApiController]
[Authorize]
public class MarketPricesController : ControllerBase
{
    private readonly IMarketPriceService _marketPriceService;

    public MarketPricesController(IMarketPriceService marketPriceService)
    {
        _marketPriceService = marketPriceService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPrices(
        [FromQuery] string? crop,
        [FromQuery] string? governorate)
    {
        var result = await _marketPriceService.GetPricesAsync(crop, governorate);
        return result.ToActionResult();
    }

    [HttpGet("series")]
    public async Task<IActionResult> GetSeries()
    {
        var result = await _marketPriceService.GetSeriesAsync();
        return result.ToActionResult();
    }

    [HttpGet("fair-hint")]
    public async Task<IActionResult> GetFairHint(
        [FromQuery] string crop,
        [FromQuery] decimal price,
        [FromQuery] string? governorate)
    {
        var result = await _marketPriceService.GetFairPriceHintAsync(crop, price, governorate);
        return result.ToActionResult();
    }
}
