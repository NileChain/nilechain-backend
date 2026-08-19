using NileChain.API.Extensions;
using NileChain.Application.Interfaces;
using NileChain.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NileChain.API.Controllers;

[ApiController]
[Authorize(Roles = $"{AppRoles.Farm},{AppRoles.Factory},{AppRoles.Admin},{AppRoles.SuperAdmin}")]
public class ContractVerificationController : ControllerBase
{
    private readonly IContractHashService _hash;

    public ContractVerificationController(IContractHashService hash) => _hash = hash;

    [HttpGet("api/contracts/{contractId:guid}/verify")]
    public async Task<IActionResult> Verify(Guid contractId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var isAdmin = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.SuperAdmin);
        var result = await _hash.VerifyAsync(contractId, Guid.Parse(userId), isAdmin);
        return result.ToActionResult();
    }
}
