using Microsoft.AspNetCore.Http;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Dispute;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Application.Notifications;
using NileChain.Application.Options;
using Microsoft.Extensions.Options;
using NileChain.Application.Validation;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;

public sealed class DisputeService : IDisputeService
{
    private const int MaxEvidenceFiles = 5;

    private readonly IDisputeRepository _disputes;
    private readonly IFarmRepository _farms;
    private readonly IFactoryRepository _factories;
    private readonly IRepository<Notification> _notifications;
    private readonly ICloudinaryService _cloudinary;
    private readonly IMockEscrowPaymentService _escrowPayments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly int _slaHours;

    public DisputeService(
        IDisputeRepository disputes,
        IFarmRepository farms,
        IFactoryRepository factories,
        IRepository<Notification> notifications,
        ICloudinaryService cloudinary,
        IMockEscrowPaymentService escrowPayments,
        IUnitOfWork unitOfWork,
        IOptions<DisputeOptions>? disputeOptions = null)
    {
        _disputes = disputes;
        _farms = farms;
        _factories = factories;
        _notifications = notifications;
        _cloudinary = cloudinary;
        _escrowPayments = escrowPayments;
        _unitOfWork = unitOfWork;
        var hours = disputeOptions?.Value.SlaHours ?? DisputeSla.DefaultHours;
        _slaHours = hours > 0 ? hours : DisputeSla.DefaultHours;
    }

    public Task<bool> HasActiveDisputeAsync(Guid contractId) =>
        _disputes.HasActiveDisputeAsync(contractId);

    public async Task<Result<DisputeDto>> OpenAsync(
        Guid userId,
        Guid contractId,
        bool asFarm,
        string type,
        string description,
        IReadOnlyList<IFormFile>? evidenceFiles)
    {
        if (string.IsNullOrWhiteSpace(description))
            return Result<DisputeDto>.Failure(DisputeErrors.DescriptionRequired);

        if (!Enum.TryParse<DisputeType>(type, ignoreCase: true, out var disputeType))
            return Result<DisputeDto>.Failure(DisputeErrors.InvalidType);

        var access = await EnsurePartyAccessAsync(userId, contractId, asFarm);
        if (access.IsFailure)
            return Result<DisputeDto>.Failure(access.Error!);

        var contract = access.Value;
        if (contract.Status != ContractStatus.Signed || !contract.IsFullySigned)
            return Result<DisputeDto>.Failure(DisputeErrors.ContractNotSigned);

        if (await _disputes.HasActiveDisputeAsync(contractId))
            return Result<DisputeDto>.Failure(DisputeErrors.ActiveExists);

        var files = (evidenceFiles ?? Array.Empty<IFormFile>())
            .Where(f => f is not null && f.Length > 0)
            .Take(MaxEvidenceFiles)
            .ToList();

        foreach (var file in files)
        {
            await using var probe = file.OpenReadStream();
            var validation = FileUploadValidation.Validate(
                file.FileName,
                file.ContentType,
                file.Length,
                probe);
            if (!validation.IsValid)
            {
                return Result<DisputeDto>.Failure(new Error(
                    validation.ErrorCode ?? DisputeErrors.EvidenceRequired.Code,
                    validation.ErrorMessage ?? DisputeErrors.EvidenceRequired.Description));
            }
        }

        var now = DateTime.UtcNow;
        var dispute = new Dispute
        {
            DisputeId = Guid.NewGuid(),
            ContractId = contractId,
            Type = disputeType,
            Status = DisputeStatus.Open,
            Description = description.Trim(),
            RaisedByParty = asFarm ? DisputeParty.Farm : DisputeParty.Factory,
            RaisedByUserId = userId,
            CreatedAt = now,
            SlaDueAt = DisputeSla.DueAt(now, _slaHours)
        };

        var openEvent = new DisputeEvent
        {
            EventId = Guid.NewGuid(),
            DisputeId = dispute.DisputeId,
            FromStatus = null,
            ToStatus = DisputeStatus.Open,
            ActorUserId = userId,
            Note = $"{dispute.RaisedByParty} opened dispute ({disputeType})",
            CreatedAt = now
        };

        await _disputes.AddAsync(dispute);
        await _disputes.AddEventAsync(openEvent);

        foreach (var file in files)
        {
            var (url, publicId) = await _cloudinary.UploadAsync(file);
            await _disputes.AddEvidenceAsync(new DisputeEvidence
            {
                DisputeEvidenceId = Guid.NewGuid(),
                DisputeId = dispute.DisputeId,
                FileName = Path.GetFileName(file.FileName),
                FileUrl = url,
                FileSize = file.Length,
                FileType = Path.GetExtension(file.FileName).TrimStart('.').ToLowerInvariant(),
                PublicId = publicId,
                UploadedAt = now
            });
        }

        await NotifyBothPartiesAsync(
            contract,
            userId,
            "Dispute opened",
            $"A dispute ({disputeType}) was opened on your supply contract and is awaiting admin review.",
            "DisputeOpened",
            NotificationRelations.Dispute,
            dispute.DisputeId);

        try
        {
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            when (UniqueConstraintViolation.IsViolation(ex))
        {
            return Result<DisputeDto>.Failure(DisputeErrors.ActiveExists);
        }

        var created = await _disputes.GetByIdAsync(dispute.DisputeId);
        return Result<DisputeDto>.Success(Map(created!));
    }

    public async Task<Result<IReadOnlyList<DisputeDto>>> ListForContractAsync(
        Guid userId,
        Guid contractId,
        bool asFarm)
    {
        var access = await EnsurePartyAccessAsync(userId, contractId, asFarm);
        if (access.IsFailure)
            return Result<IReadOnlyList<DisputeDto>>.Failure(access.Error!);

        var items = await _disputes.GetByContractIdAsync(contractId);
        return Result<IReadOnlyList<DisputeDto>>.Success(items.Select(Map).ToList());
    }

    public async Task<Result<DisputeDto>> GetAsync(Guid userId, Guid disputeId, bool asFarm)
    {
        var dispute = await _disputes.GetByIdAsync(disputeId);
        if (dispute is null)
            return Result<DisputeDto>.Failure(DisputeErrors.NotFound);

        var access = await EnsurePartyAccessAsync(userId, dispute.ContractId, asFarm);
        if (access.IsFailure)
            return Result<DisputeDto>.Failure(access.Error!);

        return Result<DisputeDto>.Success(Map(dispute));
    }

    public async Task<Result<DisputeListDto>> ListAdminAsync(
        string? status,
        string? type,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        DisputeStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse(status, ignoreCase: true, out DisputeStatus parsedStatus))
                return Result<DisputeListDto>.Failure(DisputeErrors.InvalidTransition);
            statusFilter = parsedStatus;
        }

        DisputeType? typeFilter = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!Enum.TryParse(type, ignoreCase: true, out DisputeType parsedType))
                return Result<DisputeListDto>.Failure(DisputeErrors.InvalidType);
            typeFilter = parsedType;
        }

        var (items, total) = await _disputes.ListAdminAsync(
            statusFilter,
            typeFilter,
            (page - 1) * pageSize,
            pageSize);

        return Result<DisputeListDto>.Success(new DisputeListDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(Map)
                .OrderByDescending(d => d.IsOverdue)
                .ThenBy(d => d.SlaDueAt ?? DateTime.MaxValue)
                .ToList()
        });
    }

    public async Task<Result<DisputeListDto>> ListMineAsync(
        Guid userId,
        bool asFarm,
        string? status,
        int page,
        int pageSize)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        DisputeStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse(status, ignoreCase: true, out DisputeStatus parsedStatus))
                return Result<DisputeListDto>.Failure(DisputeErrors.InvalidTransition);
            statusFilter = parsedStatus;
        }

        Guid farmId = Guid.Empty;
        Guid factoryId = Guid.Empty;
        if (asFarm)
        {
            var farm = await _farms.GetByUserIdAsync(userId);
            if (farm is null)
                return Result<DisputeListDto>.Failure(FarmErrors.FarmNotFound);
            farmId = farm.FarmId;
        }
        else
        {
            var factory = await _factories.GetByUserIdAsync(userId);
            if (factory is null)
                return Result<DisputeListDto>.Failure(FactoryErrors.FactoryNotFound);
            factoryId = factory.FactoryId;
        }

        var (items, total) = await _disputes.ListForPartyAsync(
            farmId,
            factoryId,
            asFarm,
            statusFilter,
            (page - 1) * pageSize,
            pageSize);

        return Result<DisputeListDto>.Success(new DisputeListDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items.Select(Map).ToList()
        });
    }

    public async Task<Result<DisputeDto>> GetAdminAsync(Guid disputeId)
    {
        var dispute = await _disputes.GetByIdAsync(disputeId);
        if (dispute is null)
            return Result<DisputeDto>.Failure(DisputeErrors.NotFound);

        return Result<DisputeDto>.Success(Map(dispute));
    }

    public Task<Result<DisputeDto>> MoveToUnderReviewAsync(
        Guid adminUserId,
        Guid disputeId,
        string? adminNote) =>
        TransitionAdminAsync(
            adminUserId,
            disputeId,
            DisputeStatus.UnderReview,
            adminNote,
            DisputeOutcomeFavor.None);

    public Task<Result<DisputeDto>> ResolveAsync(
        Guid adminUserId,
        Guid disputeId,
        string adminNote,
        string outcomeFavor)
    {
        if (string.IsNullOrWhiteSpace(adminNote))
            return Task.FromResult(Result<DisputeDto>.Failure(DisputeErrors.AdminNoteRequired));

        if (!Enum.TryParse<DisputeOutcomeFavor>(outcomeFavor, ignoreCase: true, out var favor)
            || favor is not (
                DisputeOutcomeFavor.Farm
                or DisputeOutcomeFavor.Factory
                or DisputeOutcomeFavor.Split))
        {
            return Task.FromResult(Result<DisputeDto>.Failure(DisputeErrors.OutcomeFavorRequired));
        }

        return TransitionAdminAsync(
            adminUserId,
            disputeId,
            DisputeStatus.Resolved,
            adminNote.Trim(),
            favor);
    }

    public Task<Result<DisputeDto>> RejectAsync(Guid adminUserId, Guid disputeId, string adminNote)
    {
        if (string.IsNullOrWhiteSpace(adminNote))
            return Task.FromResult(Result<DisputeDto>.Failure(DisputeErrors.AdminNoteRequired));

        return TransitionAdminAsync(
            adminUserId,
            disputeId,
            DisputeStatus.Rejected,
            adminNote.Trim(),
            DisputeOutcomeFavor.None);
    }

    private async Task<Result<DisputeDto>> TransitionAdminAsync(
        Guid adminUserId,
        Guid disputeId,
        DisputeStatus to,
        string? adminNote,
        DisputeOutcomeFavor outcomeFavor)
    {
        var dispute = await _disputes.GetByIdAsync(disputeId);
        if (dispute is null)
            return Result<DisputeDto>.Failure(DisputeErrors.NotFound);

        var from = dispute.Status;
        if (!DisputeTransitions.CanTransition(from, to))
            return Result<DisputeDto>.Failure(DisputeErrors.InvalidTransition);

        await using var tx = await _unitOfWork.BeginTransactionAsync();
        var now = DateTime.UtcNow;

        var ok = await _disputes.TryAtomicTransitionAsync(
            disputeId,
            from,
            to,
            now,
            adminNote,
            outcomeFavor,
            adminUserId);

        if (!ok)
            return Result<DisputeDto>.Failure(DisputeErrors.Conflict);

        if (to == DisputeStatus.Resolved)
        {
            var settle = await _escrowPayments.SettleDisputeOutcomeAsync(
                dispute.ContractId,
                adminUserId,
                outcomeFavor.ToString(),
                adminNote ?? $"Resolved in favor of {outcomeFavor}");
            if (settle.IsFailure)
                return Result<DisputeDto>.Failure(settle.Error!);
        }

        var note = BuildEventNote(to, outcomeFavor, adminNote);
        await _disputes.AddEventAsync(new DisputeEvent
        {
            EventId = Guid.NewGuid(),
            DisputeId = disputeId,
            FromStatus = from,
            ToStatus = to,
            ActorUserId = adminUserId,
            Note = note,
            CreatedAt = now
        });

        var (title, type, message) = to switch
        {
            DisputeStatus.UnderReview => (
                "Dispute under review",
                "DisputeUnderReview",
                "An admin is reviewing the dispute on your supply contract."),
            DisputeStatus.Resolved => (
                "Dispute resolved",
                "DisputeResolved",
                BuildResolvedMessage(outcomeFavor, adminNote)),
            DisputeStatus.Rejected => (
                "Dispute rejected",
                "DisputeRejected",
                $"The dispute was rejected. Note: {adminNote}"),
            _ => (
                "Dispute updated",
                "Dispute",
                $"Dispute status is now {to}.")
        };

        await NotifyBothPartiesAsync(
            dispute.Contract,
            adminUserId,
            title,
            message,
            type,
            NotificationRelations.Dispute,
            dispute.DisputeId);
        await _unitOfWork.SaveChangesAsync();
        await tx.CommitAsync();

        var updated = await _disputes.GetByIdAsync(disputeId);
        return Result<DisputeDto>.Success(Map(updated!));
    }

    private async Task<Result<Contract>> EnsurePartyAccessAsync(Guid userId, Guid contractId, bool asFarm)
    {
        if (asFarm)
        {
            var farm = await _farms.GetByUserIdAsync(userId);
            if (farm is null)
            {
                if (await _factories.GetByUserIdAsync(userId) is not null)
                    return Result<Contract>.Failure(DisputeErrors.Forbidden);
                return Result<Contract>.Failure(FarmErrors.FarmNotFound);
            }

            var contract = await _farms.GetContractForFarmAsync(userId, contractId);
            if (contract is null)
                return Result<Contract>.Failure(DisputeErrors.ContractNotFound);

            return Result<Contract>.Success(contract);
        }

        var factory = await _factories.GetByUserIdAsync(userId);
        if (factory is null)
        {
            if (await _farms.GetByUserIdAsync(userId) is not null)
                return Result<Contract>.Failure(DisputeErrors.Forbidden);
            return Result<Contract>.Failure(FactoryErrors.FactoryNotFound);
        }

        var factoryContract = await _factories.GetContractForFactoryAsync(factory.FactoryId, contractId);
        if (factoryContract is null)
            return Result<Contract>.Failure(DisputeErrors.ContractNotFound);

        return Result<Contract>.Success(factoryContract);
    }

    private async Task NotifyBothPartiesAsync(
        Contract contract,
        Guid actorUserId,
        string title,
        string message,
        string type,
        string relatedEntityType,
        Guid relatedEntityId)
    {
        var farmUserId = contract.FarmMatch?.Farm?.UserId;
        var factoryUserId = contract.FarmMatch?.SupplyRequest?.Factory?.UserId;

        foreach (var target in new[] { farmUserId, factoryUserId })
        {
            if (target is null || target == Guid.Empty)
                continue;

            await _notifications.AddAsync(new Notification
            {
                NotificationId = Guid.NewGuid(),
                UserId = target.Value,
                Title = title,
                Message = message,
                Type = type,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = relatedEntityId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            });
        }
    }

    private static string BuildEventNote(
        DisputeStatus to,
        DisputeOutcomeFavor favor,
        string? adminNote) =>
        to switch
        {
            DisputeStatus.UnderReview => string.IsNullOrWhiteSpace(adminNote)
                ? "Moved to under review"
                : $"Under review: {adminNote}",
            DisputeStatus.Resolved =>
                $"Resolved in favor of {favor.ToString().ToLowerInvariant()}. {adminNote}",
            DisputeStatus.Rejected => $"Rejected. {adminNote}",
            _ => adminNote ?? to.ToString()
        };

    private static string BuildResolvedMessage(DisputeOutcomeFavor favor, string? adminNote) =>
        $"Resolved in favor of {favor.ToString().ToLowerInvariant()}. Note: {adminNote}";

    private static DisputeDto Map(Dispute d) => new()
    {
        DisputeId = d.DisputeId,
        ContractId = d.ContractId,
        Type = d.Type.ToString(),
        Status = d.Status.ToString(),
        Description = d.Description,
        RaisedByParty = d.RaisedByParty.ToString(),
        RaisedByUserId = d.RaisedByUserId,
        AdminNote = d.AdminNote,
        OutcomeFavor = d.OutcomeFavor.ToString(),
        CreatedAt = d.CreatedAt,
        SlaDueAt = d.SlaDueAt ?? DisputeSla.DueAt(d.CreatedAt, DisputeSla.DefaultHours),
        IsOverdue = DisputeSla.IsOverdue(d, DateTime.UtcNow),
        UnderReviewAt = d.UnderReviewAt,
        ResolvedAt = d.ResolvedAt,
        RejectedAt = d.RejectedAt,
        FarmName = d.Contract?.FarmMatch?.Farm?.Name,
        FactoryName = d.Contract?.FarmMatch?.SupplyRequest?.Factory?.Name,
        FulfillmentFrozen = DisputeTransitions.IsActive(d.Status),
        Evidence = (d.Evidence ?? Array.Empty<DisputeEvidence>())
            .OrderBy(e => e.UploadedAt)
            .Select(e => new DisputeEvidenceDto
            {
                DisputeEvidenceId = e.DisputeEvidenceId,
                FileName = e.FileName,
                FileUrl = e.FileUrl,
                FileSize = e.FileSize,
                FileType = e.FileType,
                UploadedAt = e.UploadedAt
            })
            .ToList(),
        Events = (d.Events ?? Array.Empty<DisputeEvent>())
            .OrderBy(e => e.CreatedAt)
            .Select(e => new DisputeEventDto
            {
                EventId = e.EventId,
                FromStatus = e.FromStatus?.ToString(),
                ToStatus = e.ToStatus.ToString(),
                ActorUserId = e.ActorUserId,
                Note = e.Note,
                CreatedAt = e.CreatedAt
            })
            .ToList()
    };
}
