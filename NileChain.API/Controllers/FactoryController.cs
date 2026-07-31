using NileChain.API.Extensions;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NileChain.API.Controllers;

[Route("api/factory")]
[ApiController]
[Authorize]
public class FactoryController : ControllerBase
{
    private readonly IFactoryService _factoryService;

    public FactoryController(IFactoryService factoryService)
    {
        _factoryService = factoryService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetProfileAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateFactoryProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.UpdateProfileAsync(Guid.Parse(userId), request);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("requests/{requestId:guid}/matches")]
    public async Task<IActionResult> GetRequestMatches(Guid requestId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _factoryService.GetRequestMatchesAsync(Guid.Parse(userId), requestId);
        return result.ToActionResult();
    }
}
