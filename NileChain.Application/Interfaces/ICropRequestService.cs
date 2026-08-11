using NileChain.Application.Common;
using NileChain.Application.Dtos.Crop;
using NileChain.Domain.Enums;

namespace NileChain.Application.Interfaces;

public interface ICropRequestService
{
    Task<Result<CropRequestDto>> CreateAsync(Guid userId, CreateCropRequestDto dto);
    Task<Result<List<CropRequestDto>>> GetMyRequestsAsync(Guid userId);
    Task<Result<List<CropRequestDto>>> GetPendingAsync();
    Task<Result<List<CropRequestDto>>> GetAllAsync(CropRequestStatus? status);
    Task<Result<CropRequestDto>> ApproveAsync(Guid adminUserId, Guid requestId, ReviewCropRequestDto dto);
    Task<Result<CropRequestDto>> RejectAsync(Guid adminUserId, Guid requestId, ReviewCropRequestDto dto);
}
