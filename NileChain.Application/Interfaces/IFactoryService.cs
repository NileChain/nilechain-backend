using NileChain.Application.Common;
using NileChain.Application.Dtos.Factory;

namespace NileChain.Application.Interfaces;

public interface IFactoryService
{
    Task<Guid> RegisterFactoryAsync(Guid userId, string name, string governorate);
    Task<Result<FactoryProfileResponse>> GetProfileAsync(Guid userId);
    Task<Result> UpdateProfileAsync(Guid userId, UpdateFactoryProfileRequest request);
}
