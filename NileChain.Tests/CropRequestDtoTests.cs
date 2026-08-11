using NileChain.Application.Dtos.Crop;
using NileChain.Application.Errors;

namespace NileChain.Tests;

public class CropRequestDtoTests
{
    [Fact]
    public void CreateDto_HoldsSubmittedFields()
    {
        var dto = new CreateCropRequestDto
        {
            Name = "Quinoa",
            Category = "Grain",
            Description = "New crop for Upper Egypt"
        };

        Assert.Equal("Quinoa", dto.Name);
        Assert.Equal("Grain", dto.Category);
        Assert.Equal("New crop for Upper Egypt", dto.Description);
    }

    [Fact]
    public void CropRequestErrors_ExposeStableCodes()
    {
        Assert.Equal("CropRequest.NameRequired", CropRequestErrors.NameRequired.Code);
        Assert.Equal("CropRequest.CropTypeAlreadyExists", CropRequestErrors.CropTypeAlreadyExists.Code);
        Assert.Equal("CropRequest.PendingRequestAlreadyExists", CropRequestErrors.PendingRequestAlreadyExists.Code);
    }
}
