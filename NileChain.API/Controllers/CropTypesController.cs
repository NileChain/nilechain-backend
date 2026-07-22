using NileChain.Application.Dtos.Farm;
using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NileChain.API.Controllers;

[Route("api/crop-types")]
[ApiController]
[Authorize]
public class CropTypesController : ControllerBase
{
    private readonly IRepository<CropType> _cropTypeRepository;

    public CropTypesController(IRepository<CropType> cropTypeRepository)
    {
        _cropTypeRepository = cropTypeRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var cropTypes = await _cropTypeRepository.GetAllAsync();
        var result = cropTypes.Select(c => new CropTypeDto
        {
            CropTypeId = c.CropTypeId,
            Name = c.Name
        }).ToList();

        return Ok(result);
    }
}
