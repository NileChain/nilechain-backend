using NileChain.Application.Common;
using NileChain.Application.Dtos.Farm;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace NileChain.Application.Services;

public class FarmService : IFarmService
{
    private readonly IFarmRepository _farmRepository;
    private readonly IRepository<CropType> _cropTypeRepository;
    private readonly IRepository<FarmDocument> _farmDocumentRepository;
    private readonly ICloudinaryService _cloudinaryService;
    private readonly IUnitOfWork _unitOfWork;

    public FarmService(
        IFarmRepository farmRepository,
        IRepository<CropType> cropTypeRepository,
        IRepository<FarmDocument> farmDocumentRepository,
        ICloudinaryService cloudinaryService,
        IUnitOfWork unitOfWork)
    {
        _farmRepository = farmRepository;
        _cropTypeRepository = cropTypeRepository;
        _farmDocumentRepository = farmDocumentRepository;
        _cloudinaryService = cloudinaryService;
        _unitOfWork = unitOfWork;
    }

    public async Task<Guid> RegisterFarmAsync(Guid userId, string name, string governorate, decimal sizeInFeddans)
    {
        var farm = new Farm
        {
            FarmId = Guid.NewGuid(),
            UserId = userId,
            Name = name,
            Governorate = governorate,
            SizeInFeddans = sizeInFeddans
        };

        await _farmRepository.AddAsync(farm);
        await _unitOfWork.SaveChangesAsync();
        return farm.FarmId;
    }

    public async Task<Result<FarmProfileResponse>> GetProfileAsync(Guid userId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result<FarmProfileResponse>.Failure(FarmErrors.FarmNotFound);

        var response = MapToProfileResponse(farm);
        return Result<FarmProfileResponse>.Success(response);
    }

    public async Task<Result> UpdateProfileAsync(Guid userId, UpdateFarmProfileRequest request)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        farm.Name = request.Name;
        farm.Location = request.Location;
        farm.Governorate = request.Governorate;
        farm.SizeInFeddans = request.SizeInFeddans;
        farm.SoilType = request.SoilType;

        _farmRepository.Update(farm);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result<FarmDocumentDto>> AddDocumentAsync(Guid userId, IFormFile file)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result<FarmDocumentDto>.Failure(FarmErrors.FarmNotFound);

        var (url, publicId) = await _cloudinaryService.UploadAsync(file);

        var document = new FarmDocument
        {
            FarmDocumentId = Guid.NewGuid(),
            FarmId = farm.FarmId,
            FileName = file.FileName,
            FileUrl = url,
            FileSize = file.Length,
            FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(),
            PublicId = publicId
        };

        await _farmDocumentRepository.AddAsync(document);
        await _unitOfWork.SaveChangesAsync();

        var dto = MapToDocumentDto(document);
        return Result<FarmDocumentDto>.Success(dto);
    }

    public async Task<Result> DeleteDocumentAsync(Guid userId, Guid documentId)
    {
        var farm = await _farmRepository.GetByUserIdAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var document = await _farmDocumentRepository.GetByIdAsync(documentId);
        if (document is null || document.FarmId != farm.FarmId)
            return Result.Failure(FarmErrors.DocumentNotFound);

        await _cloudinaryService.DeleteAsync(document.PublicId);
        _farmDocumentRepository.Remove(document);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> AddCropAsync(Guid userId, Guid cropTypeId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var cropType = await _cropTypeRepository.GetByIdAsync(cropTypeId);
        if (cropType is null)
            return Result.Failure(FarmErrors.CropTypeNotFound);

        if (farm.CropTypes.Any(c => c.CropTypeId == cropTypeId))
            return Result.Failure(FarmErrors.CropAlreadyAdded);

        farm.CropTypes.Add(cropType);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    public async Task<Result> DeleteCropAsync(Guid userId, Guid cropTypeId)
    {
        var farm = await _farmRepository.GetFarmWithDetailsAsync(userId);
        if (farm is null)
            return Result.Failure(FarmErrors.FarmNotFound);

        var cropType = farm.CropTypes.FirstOrDefault(c => c.CropTypeId == cropTypeId);
        if (cropType is null)
            return Result.Failure(FarmErrors.CropTypeNotFound);

        farm.CropTypes.Remove(cropType);
        await _unitOfWork.SaveChangesAsync();

        return Result.Success();
    }

    private FarmProfileResponse MapToProfileResponse(Farm farm)
    {
        return new FarmProfileResponse
        {
            FarmId = farm.FarmId,
            Name = farm.Name,
            Location = farm.Location,
            Governorate = farm.Governorate,
            SizeInFeddans = farm.SizeInFeddans,
            SoilType = farm.SoilType?.ToString(),
            Phone = farm.User.PhoneNumber,
            IsVerified = farm.IsVerified,
            CompletionPercent = CalculateCompletionPercent(farm),
            CropTypes = farm.CropTypes.Select(c => new CropTypeDto
            {
                CropTypeId = c.CropTypeId,
                Name = c.Name
            }).ToList(),
            Documents = farm.FarmDocuments.Select(MapToDocumentDto).ToList()
        };
    }

    private static FarmDocumentDto MapToDocumentDto(FarmDocument document)
    {
        return new FarmDocumentDto
        {
            DocumentId = document.FarmDocumentId,
            Name = document.FileName,
            FileUrl = document.FileUrl,
            Size = FormatFileSize(document.FileSize),
            FileType = document.FileType
        };
    }

    private static int CalculateCompletionPercent(Farm farm)
    {
        var fields = 0;

        if (!string.IsNullOrWhiteSpace(farm.Name)) fields++;
        if (!string.IsNullOrWhiteSpace(farm.Location)) fields++;
        if (!string.IsNullOrWhiteSpace(farm.Governorate)) fields++;
        if (farm.SoilType is not null) fields++;
        if (farm.SizeInFeddans is not null && farm.SizeInFeddans > 0) fields++;
        if (!string.IsNullOrWhiteSpace(farm.User.PhoneNumber)) fields++;
        if (farm.CropTypes.Count > 0) fields++;
        if (farm.FarmDocuments.Count > 0) fields++;

        return (int)Math.Round((fields / 8.0) * 100);
    }

    private static string FormatFileSize(long bytes)
    {
        if (bytes < 1024)
            return $"{bytes} B";
        if (bytes < 1024 * 1024)
            return $"{bytes / 1024.0:F1} KB";
        return $"{bytes / (1024.0 * 1024.0):F1} MB";
    }
}
