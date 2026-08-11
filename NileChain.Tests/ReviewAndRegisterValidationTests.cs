using NileChain.Application.Reviews;
using NileChain.Application.Validation.Auth;
using NileChain.Application.Dtos.Auth.Requests;

namespace NileChain.Tests;

public class ReviewPartyAuthorizationTests
{
    [Fact]
    public void UnrelatedUser_CannotReview()
    {
        var farm = Guid.NewGuid();
        var factory = Guid.NewGuid();
        var stranger = Guid.NewGuid();

        var result = ReviewPartyAuthorization.Validate(stranger, farm, farm, factory);
        Assert.False(result.IsSuccess);
        Assert.Equal("Review.NotAParty", result.Error!.Code);
    }

    [Fact]
    public void FarmReviewer_MustTargetFactory()
    {
        var farm = Guid.NewGuid();
        var factory = Guid.NewGuid();

        var bad = ReviewPartyAuthorization.Validate(farm, farm, farm, factory);
        Assert.False(bad.IsSuccess);

        var ok = ReviewPartyAuthorization.Validate(farm, factory, farm, factory);
        Assert.True(ok.IsSuccess);
    }

    [Fact]
    public void FactoryReviewer_MustTargetFarm()
    {
        var farm = Guid.NewGuid();
        var factory = Guid.NewGuid();

        var ok = ReviewPartyAuthorization.Validate(factory, farm, farm, factory);
        Assert.True(ok.IsSuccess);
    }
}

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void MissingPhone_IsInvalid()
    {
        var req = ValidFarmRequest();
        req.Phone = "";
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(RegisterRequest.Phone));
    }

    [Fact]
    public void InvalidPhone_IsRejected()
    {
        var req = ValidFarmRequest();
        req.Phone = "02001234567";
        var result = _validator.Validate(req);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidEgyptianPhone_Passes()
    {
        var req = ValidFarmRequest();
        req.Phone = "01001234567";
        var result = _validator.Validate(req);
        Assert.True(result.IsValid);
    }

    private static RegisterRequest ValidFarmRequest() => new()
    {
        Email = "farm@example.com",
        Password = "Password1!",
        ConfirmPassword = "Password1!",
        BusinessType = "Farm",
        Phone = "01001234567",
        Name = "Test Farm",
        Governorate = "Giza",
        SizeInFeddans = 10
    };
}
