using NileChain.Application.Common;
using NileChain.Application.Dtos.Factory;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class FactoryService : IFactoryService
{
    private readonly IFactoryRepository _factoryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public FactoryService(
        IFactoryRepository factoryRepository,
        IUnitOfWork unitOfWork)
    {
        _factoryRepository = factoryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> RegisterFactoryAsync(Guid userId, string name, string governorate)
    {
        var factory = new Factory
        {
            FactoryId = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Governorate = governorate
        };

        await _factoryRepository.AddAsync(factory);
        await _unitOfWork.SaveChangesAsync();
        return factory.FactoryId;
    }

    public async Task<Result<FactoryProfileResponse>> GetProfileAsync(Guid userId)
    {
        var factory = await _factoryRepository.GetFactoryWithDetailsAsync(userId);
        if (factory is null)
            return Result<FactoryProfileResponse>.Failure(FactoryErrors.FactoryNotFound);

        var response = MapToProfileResponse(factory);
        return Result<FactoryProfileResponse>.Success(response);
    }

    public async Task<Result> UpdateProfileAsync(Guid userId, UpdateFactoryProfileRequest request)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result.Failure(FactoryErrors.FactoryNotFound);

        factory.Name = request.Name;
        factory.Location = request.Location;
        factory.Governorate = request.Governorate;
        factory.IndustryType = request.IndustryType;

        _factoryRepository.Update(factory);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<List<FactoryMatchItemDto>>> GetRequestMatchesAsync(Guid userId, Guid requestId)
    {
        var factory = await _factoryRepository.GetByUserIdAsync(userId);
        if (factory is null)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.FactoryNotFound);

        var supplyRequest = await _factoryRepository.GetSupplyRequestByIdAsync(requestId);
        if (supplyRequest is null)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.SupplyRequestNotFound);

        if (supplyRequest.FactoryId != factory.FactoryId)
            return Result<List<FactoryMatchItemDto>>.Failure(FactoryErrors.UnauthorizedAccess);

        var matches = await _factoryRepository.GetMatchesByRequestIdAsync(factory.FactoryId, requestId);

        var dtos = matches.Select(m => new FactoryMatchItemDto
        {
            MatchId = m.MatchId,
            FarmId = m.FarmId,
            FarmName = m.Farm?.Name ?? "Unknown",
            FarmLocation = m.Farm?.Location,
            FarmGovernorate = m.Farm?.Governorate,
            FarmIsVerified = m.Farm?.IsVerified ?? false,
            FarmAverageRating = m.Farm?.AverageRating ?? 0,
            MatchScore = m.MatchScore,
            RiskScore = m.RiskScore,
            Status = m.Status.ToString(),
            CreatedAt = m.CreatedAt
        }).ToList();

        return Result<List<FactoryMatchItemDto>>.Success(dtos);
    }

    private static FactoryProfileResponse MapToProfileResponse(Factory factory)
    {
        return new FactoryProfileResponse
        {
            FactoryId = factory.FactoryId,
            Name = factory.Name,
            Location = factory.Location,
            Governorate = factory.Governorate,
            IndustryType = factory.IndustryType,
            Phone = factory.User.PhoneNumber,
            IsVerified = factory.IsVerified,
            AverageRating = factory.AverageRating,
            RatingCount = factory.RatingCount,
            CompletionPercent = CalculateCompletionPercent(factory)
        };
    }

    private static int CalculateCompletionPercent(Factory factory)
    {
        var fields = 0;

        if (!string.IsNullOrWhiteSpace(factory.Name)) fields++;
        if (!string.IsNullOrWhiteSpace(factory.Location)) fields++;
        if (!string.IsNullOrWhiteSpace(factory.Governorate)) fields++;
        if (!string.IsNullOrWhiteSpace(factory.IndustryType)) fields++;
        if (!string.IsNullOrWhiteSpace(factory.User.PhoneNumber)) fields++;

        return (int)Math.Round((fields / 5.0) * 100);
    }
}
