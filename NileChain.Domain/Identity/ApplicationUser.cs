using NileChain.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace NileChain.Domain.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public bool IsVerified { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Farm? Farm { get; set; }
    public Factory? Factory { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; }
    = new List<RefreshToken>();
}
