using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Contracts;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Validation;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;
using NileChain.Infrastructure.Persistence;

namespace NileChain.Infrastructure.Services;

public sealed class ContractAttachmentService : IContractAttachmentService
{
    private readonly NileChainDbContext _db;
    private readonly ICloudinaryService _cloudinary;
    private readonly IUnitOfWork _uow;

    public ContractAttachmentService(
        NileChainDbContext db,
        ICloudinaryService cloudinary,
        IUnitOfWork uow)
    {
        _db = db;
        _cloudinary = cloudinary;
        _uow = uow;
    }

    public async Task<Result<List<ContractAttachmentDto>>> ListAsync(
        Guid userId,
        Guid contractId,
        bool isFactory)
    {
        var access = await ResolvePartyContractAsync(userId, contractId, isFactory);
        if (!access.IsSuccess)
            return Result<List<ContractAttachmentDto>>.Failure(access.Error!);

        var items = await _db.ContractAttachments
            .AsNoTracking()
            .Where(a => a.ContractId == contractId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync();

        return Result<List<ContractAttachmentDto>>.Success(items.Select(Map).ToList());
    }

    public async Task<Result<ContractAttachmentDto>> UploadAsync(
        Guid userId,
        Guid contractId,
        bool isFactory,
        IFormFile file,
        ContractAttachmentKind kind)
    {
        var access = await ResolvePartyContractAsync(userId, contractId, isFactory);
        if (!access.IsSuccess)
            return Result<ContractAttachmentDto>.Failure(access.Error!);

        if (file is null || file.Length <= 0)
            return Result<ContractAttachmentDto>.Failure(new Error("File.Empty", "File is required."));

        await using var probe = file.OpenReadStream();
        var validation = FileUploadValidation.Validate(
            file.FileName,
            file.ContentType,
            file.Length,
            probe);

        if (!validation.IsValid)
        {
            return Result<ContractAttachmentDto>.Failure(new Error(
                validation.ErrorCode ?? "File.Invalid",
                validation.ErrorMessage ?? "Invalid file."));
        }

        var (url, publicId) = await _cloudinary.UploadAsync(file);

        var entity = new ContractAttachment
        {
            AttachmentId = Guid.NewGuid(),
            ContractId = contractId,
            Kind = kind,
            FileName = Path.GetFileName(file.FileName),
            FileUrl = url,
            ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                ? "application/octet-stream"
                : file.ContentType,
            FileSize = file.Length,
            PublicId = publicId,
            UploadedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _db.ContractAttachments.Add(entity);
        await _uow.SaveChangesAsync();
        return Result<ContractAttachmentDto>.Success(Map(entity));
    }

    public async Task<Result> DeleteAsync(
        Guid userId,
        Guid contractId,
        Guid attachmentId,
        bool isFactory)
    {
        var access = await ResolvePartyContractAsync(userId, contractId, isFactory);
        if (!access.IsSuccess)
            return Result.Failure(access.Error!);

        var contract = access.Value!;
        var attachment = await _db.ContractAttachments
            .FirstOrDefaultAsync(a => a.AttachmentId == attachmentId && a.ContractId == contractId);

        if (attachment is null)
            return Result.Failure(new Error("Contract.AttachmentNotFound", "Attachment not found."));

        if (attachment.UploadedByUserId != userId)
        {
            return Result.Failure(new Error(
                "Contract.AttachmentForbidden",
                "You can only delete attachments you uploaded."));
        }

        if (contract.IsFullySigned)
        {
            return Result.Failure(new Error(
                "Contract.AttachmentLocked",
                "Attachments cannot be deleted after the contract is fully signed."));
        }

        _db.ContractAttachments.Remove(attachment);
        await _uow.SaveChangesAsync();
        return Result.Success();
    }

    private async Task<Result<Contract>> ResolvePartyContractAsync(
        Guid userId,
        Guid contractId,
        bool isFactory)
    {
        var contract = await _db.Contracts
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.Farm)
            .Include(c => c.FarmMatch)
                .ThenInclude(m => m.SupplyRequest)
                    .ThenInclude(r => r.Factory)
            .FirstOrDefaultAsync(c => c.ContractId == contractId);

        if (contract is null)
            return Result<Contract>.Failure(
                isFactory ? FactoryErrors.ContractNotFound : FarmErrors.ContractNotFound);

        if (isFactory)
        {
            if (contract.FarmMatch?.SupplyRequest?.Factory?.UserId != userId)
                return Result<Contract>.Failure(FactoryErrors.UnauthorizedAccess);
        }
        else if (contract.FarmMatch?.Farm?.UserId != userId)
        {
            return Result<Contract>.Failure(FarmErrors.UnauthorizedAccess);
        }

        return Result<Contract>.Success(contract);
    }

    private static ContractAttachmentDto Map(ContractAttachment a) => new()
    {
        AttachmentId = a.AttachmentId,
        ContractId = a.ContractId,
        Kind = a.Kind.ToString(),
        FileName = a.FileName,
        FileUrl = a.FileUrl,
        ContentType = a.ContentType,
        FileSize = a.FileSize,
        UploadedByUserId = a.UploadedByUserId,
        CreatedAt = a.CreatedAt
    };
}
