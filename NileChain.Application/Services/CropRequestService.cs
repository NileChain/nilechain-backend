using Microsoft.AspNetCore.Identity;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Crop;
using NileChain.Application.Dtos.Email;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Constants;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public class CropRequestService : ICropRequestService
{
    private readonly IRepository<CropRequest> _cropRequestRepository;
    private readonly IRepository<CropType> _cropTypeRepository;
    private readonly IRepository<Notification> _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService? _emailService;

    public CropRequestService(
        IRepository<CropRequest> cropRequestRepository,
        IRepository<CropType> cropTypeRepository,
        IRepository<Notification> notificationRepository,
        IUnitOfWork unitOfWork,
        UserManager<ApplicationUser> userManager,
        IEmailService? emailService = null)
    {
        _cropRequestRepository = cropRequestRepository;
        _cropTypeRepository = cropTypeRepository;
        _notificationRepository = notificationRepository;
        _unitOfWork = unitOfWork;
        _userManager = userManager;
        _emailService = emailService;
    }

    public async Task<Result<CropRequestDto>> CreateAsync(Guid userId, CreateCropRequestDto dto)
    {
        var name = dto.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            return Result<CropRequestDto>.Failure(CropRequestErrors.NameRequired);

        var cropTypes = await _cropTypeRepository.GetAllAsync();
        if (cropTypes.Any(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Result<CropRequestDto>.Failure(CropRequestErrors.CropTypeAlreadyExists);

        var requests = await _cropRequestRepository.GetAllAsync();
        if (requests.Any(r =>
                r.Status == CropRequestStatus.Pending &&
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            return Result<CropRequestDto>.Failure(CropRequestErrors.PendingRequestAlreadyExists);

        var cropRequest = new CropRequest
        {
            CropRequestId = Guid.NewGuid(),
            RequestedByUserId = userId,
            Name = name,
            Category = TrimOrNull(dto.Category),
            Description = TrimOrNull(dto.Description),
            Status = CropRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await _cropRequestRepository.AddAsync(cropRequest);

        var admins = await GetAdminUsersAsync();
        foreach (var admin in admins)
        {
            await _notificationRepository.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = admin.Id,
                Title = "New crop request",
                Message = $"A new crop type \"{name}\" was requested and awaits review.",
                Type = "CropRequest",
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        await _unitOfWork.SaveChangesAsync();

        await TryNotifyAdminsByEmailAsync(admins, name);

        return Result<CropRequestDto>.Success(Map(cropRequest));
    }

    public async Task<Result<List<CropRequestDto>>> GetMyRequestsAsync(Guid userId)
    {
        var items = (await _cropRequestRepository.GetAllAsync())
            .Where(r => r.RequestedByUserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .Select(Map)
            .ToList();

        return Result<List<CropRequestDto>>.Success(items);
    }

    public async Task<Result<List<CropRequestDto>>> GetPendingAsync()
        => await GetAllAsync(CropRequestStatus.Pending);

    public async Task<Result<List<CropRequestDto>>> GetAllAsync(CropRequestStatus? status)
    {
        var query = (await _cropRequestRepository.GetAllAsync()).AsEnumerable();

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var items = query
            .OrderByDescending(r => r.CreatedAt)
            .Select(Map)
            .ToList();

        return Result<List<CropRequestDto>>.Success(items);
    }

    public async Task<Result<CropRequestDto>> ApproveAsync(
        Guid adminUserId,
        Guid requestId,
        ReviewCropRequestDto dto)
    {
        var cropRequest = await _cropRequestRepository.GetByIdAsync(requestId);
        if (cropRequest is null)
            return Result<CropRequestDto>.Failure(CropRequestErrors.NotFound);

        if (cropRequest.Status != CropRequestStatus.Pending)
            return Result<CropRequestDto>.Failure(CropRequestErrors.NotPending);

        var finalName = !string.IsNullOrWhiteSpace(dto.Name)
            ? dto.Name.Trim()
            : cropRequest.Name;

        if (string.IsNullOrWhiteSpace(finalName))
            return Result<CropRequestDto>.Failure(CropRequestErrors.NameRequired);

        cropRequest.Name = finalName;

        if (dto.Category is not null)
            cropRequest.Category = TrimOrNull(dto.Category);
        if (dto.Description is not null)
            cropRequest.Description = TrimOrNull(dto.Description);
        if (dto.AdminNotes is not null)
            cropRequest.AdminNotes = TrimOrNull(dto.AdminNotes);

        var cropTypes = await _cropTypeRepository.GetAllAsync();
        var cropType = cropTypes.FirstOrDefault(c =>
            string.Equals(c.Name, finalName, StringComparison.OrdinalIgnoreCase));

        if (cropType is null)
        {
            cropType = new CropType
            {
                CropTypeId = Guid.NewGuid(),
                Name = finalName
            };
            await _cropTypeRepository.AddAsync(cropType);
        }

        cropRequest.Status = CropRequestStatus.Approved;
        cropRequest.ApprovedCropTypeId = cropType.CropTypeId;
        cropRequest.ReviewedByUserId = adminUserId;
        cropRequest.ReviewedAt = DateTime.UtcNow;
        _cropRequestRepository.Update(cropRequest);

        await _notificationRepository.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = cropRequest.RequestedByUserId,
            Title = "Crop request approved",
            Message = $"Your crop request \"{finalName}\" was approved and is now available as a crop type.",
            Type = "CropRequest",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync();
        return Result<CropRequestDto>.Success(Map(cropRequest));
    }

    public async Task<Result<CropRequestDto>> RejectAsync(
        Guid adminUserId,
        Guid requestId,
        ReviewCropRequestDto dto)
    {
        var cropRequest = await _cropRequestRepository.GetByIdAsync(requestId);
        if (cropRequest is null)
            return Result<CropRequestDto>.Failure(CropRequestErrors.NotFound);

        if (cropRequest.Status != CropRequestStatus.Pending)
            return Result<CropRequestDto>.Failure(CropRequestErrors.NotPending);

        if (dto.AdminNotes is not null)
            cropRequest.AdminNotes = TrimOrNull(dto.AdminNotes);

        cropRequest.Status = CropRequestStatus.Rejected;
        cropRequest.ReviewedByUserId = adminUserId;
        cropRequest.ReviewedAt = DateTime.UtcNow;
        _cropRequestRepository.Update(cropRequest);

        var notesSuffix = string.IsNullOrWhiteSpace(cropRequest.AdminNotes)
            ? string.Empty
            : $" Notes: {cropRequest.AdminNotes}";

        await _notificationRepository.AddAsync(new Notification
        {
            NotificationId = Guid.NewGuid(),
            UserId = cropRequest.RequestedByUserId,
            Title = "Crop request rejected",
            Message = $"Your crop request \"{cropRequest.Name}\" was rejected.{notesSuffix}",
            Type = "CropRequest",
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        });

        await _unitOfWork.SaveChangesAsync();
        return Result<CropRequestDto>.Success(Map(cropRequest));
    }

    private async Task<List<ApplicationUser>> GetAdminUsersAsync()
    {
        var admins = await _userManager.GetUsersInRoleAsync(AppRoles.Admin);
        var superAdmins = await _userManager.GetUsersInRoleAsync(AppRoles.SuperAdmin);

        return admins
            .Concat(superAdmins)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();
    }

    private async Task TryNotifyAdminsByEmailAsync(IEnumerable<ApplicationUser> admins, string cropName)
    {
        if (_emailService is null)
            return;

        foreach (var admin in admins)
        {
            if (string.IsNullOrWhiteSpace(admin.Email))
                continue;

            try
            {
                await _emailService.SendAsync(new EmailMessage
                {
                    To = admin.Email,
                    Subject = "New crop request",
                    Body = $"A new crop type \"{cropName}\" was requested and awaits admin review.",
                    IsHtml = false
                });
            }
            catch
            {
                // Email is best-effort; never fail the crop request.
            }
        }
    }

    private static string? TrimOrNull(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static CropRequestDto Map(CropRequest entity) => new()
    {
        CropRequestId = entity.CropRequestId,
        RequestedByUserId = entity.RequestedByUserId,
        Name = entity.Name,
        Category = entity.Category,
        Description = entity.Description,
        Status = entity.Status.ToString(),
        AdminNotes = entity.AdminNotes,
        ReviewedByUserId = entity.ReviewedByUserId,
        ApprovedCropTypeId = entity.ApprovedCropTypeId,
        CreatedAt = entity.CreatedAt,
        ReviewedAt = entity.ReviewedAt
    };
}
