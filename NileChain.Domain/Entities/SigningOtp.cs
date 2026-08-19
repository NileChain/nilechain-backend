using NileChain.Domain.Identity;

namespace NileChain.Domain.Entities;

public class SigningOtp
{
    public Guid Id { get; set; }
    public Guid ContractId { get; set; }
    public Guid UserId { get; set; }

    /// <summary>SHA-256 hex of the 6-digit OTP. Never store plaintext.</summary>
    public string OtpHash { get; set; } = default!;

    public DateTime ExpiresAt { get; set; }
    public bool IsUsed { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contract Contract { get; set; } = default!;
    public ApplicationUser User { get; set; } = default!;
}
