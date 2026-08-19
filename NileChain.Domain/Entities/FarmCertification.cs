namespace NileChain.Domain.Entities;

public class FarmCertification
{
    public Guid FarmId { get; set; }
    public Guid CertificationId { get; set; }
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }

    /// <summary>Null when leftover self-serve rows exist. Only admin-granted certs count toward trust.</summary>
    public Guid? GrantedByAdminUserId { get; set; }

    public Farm Farm { get; set; } = default!;
    public Certification Certification { get; set; } = default!;
}
