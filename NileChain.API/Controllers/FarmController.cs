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

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetDocumentsAsync(Guid.Parse(userId));
        return result.ToActionResult();
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

    [HttpGet("matches")]
    public async Task<IActionResult> GetMatches([FromQuery] string? status, [FromQuery] Guid? cropTypeId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetMatchesAsync(Guid.Parse(userId), status, cropTypeId);
        return result.ToActionResult();
    }

    [HttpPut("matches/{matchId:guid}/respond")]
    public async Task<IActionResult> RespondToMatch(Guid matchId, [FromBody] RespondToMatchRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.RespondToMatchAsync(Guid.Parse(userId), matchId, request.Action);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("contracts")]
    public async Task<IActionResult> GetContracts()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetContractsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetConversationsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpGet("conversations/{matchId:guid}/messages")]
    public async Task<IActionResult> GetMessages(Guid matchId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetMessagesAsync(Guid.Parse(userId), matchId);
        return result.ToActionResult();
    }

    [HttpPost("conversations/{matchId:guid}/messages")]
    public async Task<IActionResult> SendMessage(Guid matchId, [FromBody] NileChain.Application.Dtos.Farm.SendMessageRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.SendMessageAsync(Guid.Parse(userId), matchId, request.Content);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }

    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.GetNotificationsAsync(Guid.Parse(userId));
        return result.ToActionResult();
    }

    [HttpPut("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationAsRead(Guid notificationId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _farmService.MarkNotificationAsReadAsync(Guid.Parse(userId), notificationId);
        return result.IsSuccess ? NoContent() : result.ToActionResult();
    }
}
