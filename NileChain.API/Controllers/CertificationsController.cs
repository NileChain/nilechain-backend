using NileChain.Application.Dtos.Farm;
using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NileChain.API.Controllers;

[Route("api/certifications")]
[ApiController]
[Authorize]
public class CertificationsController : ControllerBase
{
    private readonly IRepository<Certification> _certificationRepository;

    public CertificationsController(IRepository<Certification> certificationRepository)
    {
        _certificationRepository = certificationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var certifications = await _certificationRepository.GetAllAsync();
        var result = certifications
            .OrderBy(c => c.Name)
            .Select(c => new CertificationCatalogItemDto
            {
                CertificationId = c.CertificationId,
                Name = c.Name
            }).ToList();

        return Ok(result);
    }
}
