using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Domain.Identity;

namespace NileChain.Application.Auth;

/// <summary>
/// Shared account-gate checks for login / refresh (unit-testable).
/// </summary>
public static class AuthAccessPolicy
{
    public static Error? DenyIfCannotAuthenticate(ApplicationUser user, bool isLockedOut)
    {
        if (!user.IsActive)
            return AuthErrors.AccountInactive;

        if (isLockedOut)
            return AuthErrors.AccountLocked;

        return null;
    }

    public static Result OkOrFail(ApplicationUser user, bool isLockedOut)
    {
        var error = DenyIfCannotAuthenticate(user, isLockedOut);
        return error is null ? Result.Success() : Result.Failure(error);
    }
}
