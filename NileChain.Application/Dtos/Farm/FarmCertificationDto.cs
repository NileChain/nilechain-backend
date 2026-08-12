namespace NileChain.Application.Dtos.Farm;

public class FarmCertificationDto
{
    public Guid CertificationId { get; set; }
    public string Name { get; set; } = default!;
    public DateTime IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsExpired { get; set; }
}

public class AddFarmCertificationRequest
{
    public Guid CertificationId { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public class CertificationCatalogItemDto
{
    public Guid CertificationId { get; set; }
    public string Name { get; set; } = default!;
}
