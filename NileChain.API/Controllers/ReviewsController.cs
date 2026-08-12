using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NileChain.API.Extensions;
using NileChain.Application.Dtos.Review;
using NileChain.Application.Interfaces;

namespace NileChain.API.Controllers;

[Route("api/reviews")]
[ApiController]
[Authorize]
public class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateReviewRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var result = await _reviewService.CreateReviewAsync(Guid.Parse(userId), request);
        return result.ToActionResult();
    }

    [HttpGet("contract/{contractId:guid}")]
    public async Task<IActionResult> GetForContract(Guid contractId)
    {
        var result = await _reviewService.GetReviewsForContractAsync(contractId);
        return result.ToActionResult();
    }

    [HttpGet("target/{targetId:guid}")]
    public async Task<IActionResult> GetForTarget(Guid targetId)
    {
        var result = await _reviewService.GetReviewsForTargetAsync(targetId);
        return result.ToActionResult();
    }
}
