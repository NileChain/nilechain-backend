using NileChain.Application.Interfaces;
using NileChain.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NileChain.API.Controllers;

[ApiController]
public class ContractIntegrityController : ControllerBase
{
    private readonly IContractIntegrityService _integrity;

    public ContractIntegrityController(IContractIntegrityService integrity) =>
        _integrity = integrity;

    /// <summary>Public verify-by-hash page. No login required.</summary>
    [AllowAnonymous]
    [HttpGet("api/public/verify/{hash}")]
    public async Task<IActionResult> Verify(string hash)
    {
        var result = await _integrity.VerifyByHashAsync(hash);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("api/contracts/{contractId:guid}/integrity")]
    public async Task<IActionResult> GetForContract(Guid contractId)
    {
        var result = await _integrity.GetActiveForContractAsync(contractId);
        return result.ToActionResult();
    }
}
