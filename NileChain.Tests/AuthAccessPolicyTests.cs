using NileChain.Application.Auth;
using NileChain.Application.Errors;
using NileChain.Domain.Identity;

namespace NileChain.Tests;

public class AuthAccessPolicyTests
{
    [Fact]
    public void DenyIfCannotAuthenticate_InactiveUser()
    {
        var user = new ApplicationUser { IsActive = false };
        var error = AuthAccessPolicy.DenyIfCannotAuthenticate(user, isLockedOut: false);
        Assert.Equal(AuthErrors.AccountInactive, error);
    }

    [Fact]
    public void DenyIfCannotAuthenticate_LockedOutUser()
    {
        var user = new ApplicationUser { IsActive = true };
        var error = AuthAccessPolicy.DenyIfCannotAuthenticate(user, isLockedOut: true);
        Assert.Equal(AuthErrors.AccountLocked, error);
    }

    [Fact]
    public void DenyIfCannotAuthenticate_ActiveUnlocked_Allows()
    {
        var user = new ApplicationUser { IsActive = true };
        var error = AuthAccessPolicy.DenyIfCannotAuthenticate(user, isLockedOut: false);
        Assert.Null(error);
    }

    [Fact]
    public void InactiveTakesPrecedenceOverLockoutMessage()
    {
        var user = new ApplicationUser { IsActive = false };
        var error = AuthAccessPolicy.DenyIfCannotAuthenticate(user, isLockedOut: true);
        Assert.Equal(AuthErrors.AccountInactive, error);
    }
}
