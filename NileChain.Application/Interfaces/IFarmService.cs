using NileChain.Application.Common;
using NileChain.Application.Dtos.Farm;
using Microsoft.AspNetCore.Http;

namespace NileChain.Application.Interfaces;

public interface IFarmService
{
    Task<Guid> RegisterFarmAsync(Guid userId, string name, string governorate, decimal sizeInFeddans);
    Task<Result<FarmProfileResponse>> GetProfileAsync(Guid userId);
    Task<Result> UpdateProfileAsync(Guid userId, UpdateFarmProfileRequest request);
    Task<Result<FarmDocumentDto>> AddDocumentAsync(Guid userId, IFormFile file);
    Task<Result> DeleteDocumentAsync(Guid userId, Guid documentId);
    Task<Result> AddCropAsync(Guid userId, Guid cropTypeId);
    Task<Result> DeleteCropAsync(Guid userId, Guid cropTypeId);
}
