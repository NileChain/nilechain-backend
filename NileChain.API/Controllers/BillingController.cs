using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.API.Extensions;
using NileChain.Application.Interfaces;
using NileChain.Domain.Constants;

namespace NileChain.API.Controllers;

[Route("api/billing")]
[ApiController]
[Authorize(Roles = $"{AppRoles.Factory},{AppRoles.Farm}")]
public class BillingController : ControllerBase
{
    private readonly ISubscriptionService _subscriptions;

    public BillingController(ISubscriptionService subscriptions) => _subscriptions = subscriptions;

    [HttpGet("me")]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var result = await _subscriptions.GetMineAsync(Guid.Parse(userId), AsFarm(), cancellationToken);
        return result.ToActionResult();
    }

    [HttpPost("subscribe")]
    public async Task<IActionResult> Subscribe(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null) return Unauthorized();
        var result = await _subscriptions.SubscribeAsync(Guid.Parse(userId), AsFarm(), cancellationToken);
        return result.ToActionResult();
    }

    private bool AsFarm() => User.IsInRole(AppRoles.Farm);
}
