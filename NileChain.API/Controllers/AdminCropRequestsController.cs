using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.API.Extensions;
using NileChain.Application.Dtos.Crop;
using NileChain.Application.Interfaces;
using NileChain.Domain.Constants;
using NileChain.Domain.Enums;

namespace NileChain.API.Controllers;

[Route("api/admin/crop-requests")]
[ApiController]
[Authorize(Roles = AppRoles.Admin + "," + AppRoles.SuperAdmin)]
public class AdminCropRequestsController : ControllerBase
{
    private readonly ICropRequestService _cropRequestService;

    public AdminCropRequestsController(ICropRequestService cropRequestService)
    {
        _cropRequestService = cropRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] string? status)
    {
        CropRequestStatus? filter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<CropRequestStatus>(status, ignoreCase: true, out var parsed))
                return BadRequest(new { Code = "CropRequest.InvalidStatus", Message = "Status must be Pending, Approved, or Rejected." });

            filter = parsed;
        }

        var result = await _cropRequestService.GetAllAsync(filter);
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ReviewCropRequestDto? dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _cropRequestService.ApproveAsync(
            Guid.Parse(userId),
            id,
            dto ?? new ReviewCropRequestDto());
        return result.ToActionResult();
    }

    [HttpPut("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReviewCropRequestDto? dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _cropRequestService.RejectAsync(
            Guid.Parse(userId),
            id,
            dto ?? new ReviewCropRequestDto());
        return result.ToActionResult();
    }
}
