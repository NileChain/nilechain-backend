using NileChain.Domain.Entities;

namespace NileChain.Domain.Common;

public static class FarmCertificationRules
{
    public static bool CountsTowardTrust(FarmCertification certification, DateTime utcNow)
    {
        if (certification.GrantedByAdminUserId is null || certification.GrantedByAdminUserId == Guid.Empty)
            return false;

        return certification.ExpiresAt is null || certification.ExpiresAt > utcNow;
    }
}
