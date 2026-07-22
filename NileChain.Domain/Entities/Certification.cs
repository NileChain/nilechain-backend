namespace NileChain.Domain.Entities;

public class Certification
{
    public Guid CertificationId { get; set; }
    public string Name { get; set; } = default!;

    public ICollection<FarmCertification> FarmCertifications { get; set; } = new List<FarmCertification>();
}
