using NileChain.API.Extensions;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NileChain.API.Controllers;

[Route("api/farm")]
[ApiController]
[Authorize]
public class FarmController : ControllerBase
{
    private readonly IFarmService _farmService;

    public FarmController(IFarmService farmService)
    {
        _farmService = farmService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetProfileAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetDashboardAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateFarmProfileRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.UpdateProfileAsync(Guid.Parse(userId), request);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("documents")]
    public async Task<IActionResult> AddDocument(IFormFile file)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.AddDocumentAsync(Guid.Parse(userId), file);
        return result.ToActionResult();
    }

    [HttpDelete("documents/{documentId:guid}")]
    public async Task<IActionResult> DeleteDocument(Guid documentId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.DeleteDocumentAsync(Guid.Parse(userId), documentId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpPost("crops")]
    public async Task<IActionResult> AddCrop(AddCropRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.AddCropAsync(Guid.Parse(userId), request.CropTypeId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpDelete("crops/{cropTypeId:guid}")]
    public async Task<IActionResult> DeleteCrop(Guid cropTypeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.DeleteCropAsync(Guid.Parse(userId), cropTypeId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
}
