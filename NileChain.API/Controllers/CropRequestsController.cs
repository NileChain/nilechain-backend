using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.API.Extensions;
using NileChain.Application.Dtos.Crop;
using NileChain.Application.Interfaces;

namespace NileChain.API.Controllers;

[Route("api/crop-requests")]
[ApiController]
[Authorize]
public class CropRequestsController : ControllerBase
{
    private readonly ICropRequestService _cropRequestService;

    public CropRequestsController(ICropRequestService cropRequestService)
    {
        _cropRequestService = cropRequestService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCropRequestDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _cropRequestService.CreateAsync(Guid.Parse(userId), dto);
        return result.ToActionResult();
    }

    [HttpGet("mine")]
    public async Task<IActionResult> GetMine()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _cropRequestService.GetMyRequestsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }
}
