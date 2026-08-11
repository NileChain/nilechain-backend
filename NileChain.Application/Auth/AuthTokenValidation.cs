using NileChain.Domain.Identity;

namespace NileChain.Application.Auth;

/// <summary>
/// JWT bearer post-validation gate (unit-testable).
/// Rejects inactive or locked-out users so access tokens stop authorizing mid-lifetime.
/// </summary>
public static class AuthTokenValidation
{
    public static bool IsAccessAllowed(ApplicationUser user, bool isLockedOut)
    {
        if (!user.IsActive)
            return false;

        if (isLockedOut)
            return false;

        return true;
    }
}
