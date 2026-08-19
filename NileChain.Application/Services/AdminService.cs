using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NileChain.Application.Admin;
using NileChain.Application.Auth;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Email;
using NileChain.Application.Email;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;
using System.Text.Json;

namespace NileChain.Application.Services
{
    public class AdminService : IAdminService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IFarmRepository _farmRepository;
        private readonly IFactoryRepository _factoryRepository;
        private readonly IRepository<NileChain.Domain.Entities.RagDocument> _ragDocumentRepository;
        private readonly IRepository<NileChain.Domain.Entities.Certification> _certificationRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IAdminAnalyticsRepository _analytics;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRagIndexer? _ragIndexer;
        private readonly IKybVerificationAgent? _kybVerificationAgent;
        private readonly IEmailService? _emailService;
        private readonly ITemplateRenderer? _templateRenderer;
        private readonly IUserAccountDeletionService _userAccountDeletionService;
        private readonly IRepository<KybDecision>? _kybDecisions;
        private readonly ISubscriptionRepository? _subscriptions;
        private readonly AppOptions _appOptions;

        public AdminService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IFarmRepository farmRepository,
            IFactoryRepository factoryRepository,
            IRepository<NileChain.Domain.Entities.RagDocument> ragDocumentRepository,
            IRepository<NileChain.Domain.Entities.Certification> certificationRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IAdminAnalyticsRepository analytics,
            IUnitOfWork unitOfWork,
            IUserAccountDeletionService userAccountDeletionService,
            IRagIndexer? ragIndexer = null,
            IKybVerificationAgent? kybVerificationAgent = null,
            IEmailService? emailService = null,
            ITemplateRenderer? templateRenderer = null,
            IOptions<AppOptions>? appOptions = null,
            IRepository<KybDecision>? kybDecisions = null,
            ISubscriptionRepository? subscriptions = null)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _farmRepository = farmRepository;
            _factoryRepository = factoryRepository;
            _ragDocumentRepository = ragDocumentRepository;
            _certificationRepository = certificationRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _analytics = analytics;
            _unitOfWork = unitOfWork;
            _userAccountDeletionService = userAccountDeletionService;
            _ragIndexer = ragIndexer;
            _kybVerificationAgent = kybVerificationAgent;
            _emailService = emailService;
            _templateRenderer = templateRenderer;
            _kybDecisions = kybDecisions;
            _subscriptions = subscriptions;
            _appOptions = appOptions?.Value ?? new AppOptions();
        }

        public async Task<PagedResult<UserListItem>> GetUsersAsync(string? role, bool? isVerified, string? search, int page, int pageSize)
        {
            var query = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(role) && !role.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var usersInRole = await _userManager.GetUsersInRoleAsync(role);
                var userIds = usersInRole.Select(u => u.Id).ToHashSet();
                query = query.Where(u => userIds.Contains(u.Id));
            }

            if (isVerified == true)
            {
                query = query.Where(u => u.IsVerified);
            }
            else if (isVerified == false)
            {
                query = query.Where(u =>
                    !u.IsVerified && u.KybReviewStatus != KybReviewStatus.Rejected);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                query = query.Where(u =>
                    u.Email!.ToLower().Contains(term) ||
                    u.UserName!.ToLower().Contains(term));
            }

            var totalCount = await query.CountAsync();

            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Include(u => u.Farm)
                .Include(u => u.Factory)
                .ToListAsync();

            var items = new List<UserListItem>();
            var latestReports = await _analytics.GetLatestKybReportsAsync(users.Select(u => u.Id).ToList());
            var latestPlans = _subscriptions is null
                ? new Dictionary<Guid, Subscription>()
                : await _subscriptions.GetLatestForUsersAsync(users.Select(u => u.Id).ToList());
            var now = DateTime.UtcNow;
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var roleName = roles.FirstOrDefault() ?? "User";
                var isBlocked = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
                latestReports.TryGetValue(user.Id, out var lastReport);
                latestPlans.TryGetValue(user.Id, out var plan);

                items.Add(MapUserListItem(user, roleName, isBlocked, lastReport, plan, now));
            }

            return new PagedResult<UserListItem>
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<UserListItem> CreateUserAsync(CreateUserRequest request)
        {
            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser is not null)
                throw new InvalidOperationException("An account with this email already exists.");

            var role = AdminRoleAllowlist.NormalizeOrThrow(request.Role);

            if (!await _roleManager.RoleExistsAsync(role))
                throw new InvalidOperationException("The selected role does not exist.");

            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow,
                IsVerified = role == "Admin",
                KybReviewStatus = role == "Admin" ? KybReviewStatus.Approved : KybReviewStatus.Pending
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException(errors);
            }

            await _userManager.AddToRoleAsync(user, role);

            if (role == "Farm" && !string.IsNullOrWhiteSpace(request.Name))
            {
                await _farmRepository.AddAsync(new Domain.Entities.Farm
                {
                    FarmId = Guid.NewGuid(),
                    UserId = user.Id,
                    Name = request.Name,
                    Governorate = request.Governorate,
                    SizeInFeddans = request.SizeInFeddans
                });
                await _unitOfWork.SaveChangesAsync();
            }
            else if (role == "Factory" && !string.IsNullOrWhiteSpace(request.Name))
            {
                await _factoryRepository.AddAsync(new Domain.Entities.Factory
                {
                    FactoryId = Guid.NewGuid(),
                    UserId = user.Id,
                    Name = request.Name,
                    Governorate = request.Governorate
                });
                await _unitOfWork.SaveChangesAsync();
            }

            return new UserListItem
            {
                Id = user.Id,
                Email = user.Email!,
                DisplayName = request.Name ?? user.UserName,
                Role = role,
                IsVerified = user.IsVerified,
                IsBlocked = false,
                CreatedAt = user.CreatedAt,
                FarmName = role == "Farm" ? request.Name : null,
                FactoryName = role == "Factory" ? request.Name : null,
                KybReviewStatus = user.KybReviewStatus.ToString()
            };
        }

        public async Task<UserListItem> UpdateUserAsync(Guid userId, UpdateUserRequest request)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                throw new InvalidOperationException("User not found.");

            var currentRoles = await _userManager.GetRolesAsync(user);
            var currentRole = currentRoles.FirstOrDefault() ?? "";

            if (!string.IsNullOrWhiteSpace(request.Role) &&
                !request.Role.Equals(currentRole, StringComparison.OrdinalIgnoreCase))
            {
                var newRole = AdminRoleAllowlist.NormalizeOrThrow(request.Role);

                if (!await _roleManager.RoleExistsAsync(newRole))
                    throw new InvalidOperationException("The selected role does not exist.");

                await _userManager.RemoveFromRoleAsync(user, currentRole);

                if (currentRole == "Farm")
                {
                    var farm = await _farmRepository.GetByUserIdAsync(userId);
                    if (farm is not null)
                    {
                        var farmContracts = await _farmRepository.GetFarmContractsAsync(userId);
                        EntityDeleteGuards.EnsureCanRemoveFarm(farmContracts.Count > 0);
                        _farmRepository.Remove(farm);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                else if (currentRole == "Factory")
                {
                    var factory = await _factoryRepository.GetByUserIdAsync(userId);
                    if (factory is not null)
                    {
                        var factoryContracts = await _factoryRepository.GetContractsAsync(factory.FactoryId);
                        EntityDeleteGuards.EnsureCanRemoveFactory(factoryContracts.Count > 0);
                        _factoryRepository.Remove(factory);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }

                await _userManager.AddToRoleAsync(user, newRole);

                if (newRole == "Farm")
                {
                    await _farmRepository.AddAsync(new Domain.Entities.Farm
                    {
                        FarmId = Guid.NewGuid(),
                        UserId = userId,
                        Name = request.Name ?? user.UserName ?? "",
                        Governorate = request.Governorate,
                        SizeInFeddans = request.SizeInFeddans
                    });
                    await _unitOfWork.SaveChangesAsync();
                }
                else if (newRole == "Factory")
                {
                    await _factoryRepository.AddAsync(new Domain.Entities.Factory
                    {
                        FactoryId = Guid.NewGuid(),
                        UserId = userId,
                        Name = request.Name ?? user.UserName ?? "",
                        Governorate = request.Governorate
                    });
                    await _unitOfWork.SaveChangesAsync();
                }

                currentRole = newRole;
            }

            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                if (currentRole == "Farm")
                {
                    var farm = await _farmRepository.GetByUserIdAsync(userId);
                    if (farm is not null)
                    {
                        farm.Name = request.Name;
                        _farmRepository.Update(farm);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                else if (currentRole == "Factory")
                {
                    var factory = await _factoryRepository.GetByUserIdAsync(userId);
                    if (factory is not null)
                    {
                        factory.Name = request.Name;
                        _factoryRepository.Update(factory);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
            }

            if (request.IsVerified.HasValue && request.IsVerified.Value != user.IsVerified)
            {
                user.IsVerified = request.IsVerified.Value;
                await _userManager.UpdateAsync(user);

                if (currentRole == "Farm")
                {
                    var farm = await _farmRepository.GetByUserIdAsync(userId);
                    if (farm is not null)
                    {
                        farm.IsVerified = request.IsVerified.Value;
                        _farmRepository.Update(farm);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                else if (currentRole == "Factory")
                {
                    var factory = await _factoryRepository.GetByUserIdAsync(userId);
                    if (factory is not null)
                    {
                        factory.IsVerified = request.IsVerified.Value;
                        _factoryRepository.Update(factory);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
            }

            var finalRoles = await _userManager.GetRolesAsync(user);
            var isBlocked = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

            var farmEnt = await _farmRepository.GetByUserIdAsync(userId);
            var factoryEnt = await _factoryRepository.GetByUserIdAsync(userId);

            return new UserListItem
            {
                Id = user.Id,
                Email = user.Email ?? "",
                DisplayName = farmEnt?.Name ?? factoryEnt?.Name ?? request.Name ?? user.UserName,
                Role = finalRoles.FirstOrDefault() ?? currentRole,
                IsVerified = user.IsVerified,
                IsBlocked = isBlocked,
                    CreatedAt = user.CreatedAt,
                    FarmId = farmEnt?.FarmId,
                    FarmName = farmEnt?.Name,
                FactoryId = factoryEnt?.FactoryId,
                FactoryName = factoryEnt?.Name,
                KybReviewStatus = user.KybReviewStatus.ToString(),
                KybAdminNote = user.KybAdminNote
            };
        }

        public Task<Result<VerifyUserResult>> VerifyUserAsync(Guid userId) =>
            AnalyzeKybAsync(userId);

        public async Task<Result<VerifyUserResult>> AnalyzeKybAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result<VerifyUserResult>.Failure(AdminErrors.UserNotFound);

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Farm"))
            {
                if (_kybVerificationAgent is null)
                    return Result<VerifyUserResult>.Failure(new Error(
                        "Admin.KybAgentMissing",
                        "KYB verification agent is not configured."));

                var farm = await _farmRepository.GetFarmWithDetailsAsync(userId)
                    ?? await _farmRepository.GetByUserIdAsync(userId);

                if (farm is null)
                    return Result<VerifyUserResult>.Failure(new Error(
                        "Admin.FarmNotFound",
                        "Farm profile for this user was not found."));

                return await _kybVerificationAgent.VerifyFarmAsync(userId, farm);
            }

            if (roles.Contains("Factory"))
            {
                if (_kybVerificationAgent is null)
                    return Result<VerifyUserResult>.Failure(new Error(
                        "Admin.KybAgentMissing",
                        "KYB verification agent is not configured."));

                var factory = await _factoryRepository.GetFactoryWithDetailsAsync(userId)
                    ?? await _factoryRepository.GetByUserIdAsync(userId);

                if (factory is null)
                    return Result<VerifyUserResult>.Failure(new Error(
                        "Admin.FactoryNotFound",
                        "Factory profile for this user was not found."));

                return await _kybVerificationAgent.VerifyFactoryAsync(userId, factory);
            }

            return Result<VerifyUserResult>.Failure(AdminErrors.KybUnsupportedRole);
        }

        public async Task<Result<VerifyUserResult>> GetLastKybReportAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result<VerifyUserResult>.Failure(AdminErrors.UserNotFound);

            var row = await _analytics.GetLatestKybReportAsync(userId);
            if (row is null)
                return Result<VerifyUserResult>.Failure(AdminErrors.KybReportNotFound);

            return Result<VerifyUserResult>.Success(MapReport(row));
        }

        public async Task<Result> ApproveUserAsync(Guid adminUserId, Guid userId, string? reason)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AdminErrors.UserNotFound);

            if (user.IsVerified)
                return Result.Failure(AdminErrors.UserAlreadyVerified);

            var last = await _analytics.GetLatestKybReportAsync(userId);
            var score = last?.TrustScore ?? 0;
            if (score < 70 && string.IsNullOrWhiteSpace(reason))
                return Result.Failure(AdminErrors.KybReasonRequired);

            var roles = await _userManager.GetRolesAsync(user);
            user.IsVerified = true;
            user.KybReviewStatus = KybReviewStatus.Approved;
            user.KybAdminNote = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
            user.KybReviewedAt = DateTime.UtcNow;
            user.KybReviewedByUserId = adminUserId;
            await _userManager.UpdateAsync(user);

            if (roles.Contains("Farm"))
            {
                var farm = await _farmRepository.GetByUserIdAsync(userId);
                if (farm is not null)
                {
                    farm.IsVerified = true;
                    _farmRepository.Update(farm);
                }
            }
            else if (roles.Contains("Factory"))
            {
                var factory = await _factoryRepository.GetByUserIdAsync(userId);
                if (factory is not null)
                {
                    factory.IsVerified = true;
                    _factoryRepository.Update(factory);
                }
            }

            await RecordDecisionAsync(userId, adminUserId, KybDecisionAction.Approved, reason ?? string.Empty, score);
            await _unitOfWork.SaveChangesAsync();
            await TrySendApprovalEmailAsync(user);
            return Result.Success();
        }

        public async Task<Result> RequestKybInfoAsync(Guid adminUserId, Guid userId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure(AdminErrors.KybReasonRequired);

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AdminErrors.UserNotFound);

            var note = reason.Trim();
            var last = await _analytics.GetLatestKybReportAsync(userId);
            user.IsVerified = false;
            user.KybReviewStatus = KybReviewStatus.RequestInfo;
            user.KybAdminNote = note;
            user.KybReviewedAt = DateTime.UtcNow;
            user.KybReviewedByUserId = adminUserId;
            await _userManager.UpdateAsync(user);

            await RecordDecisionAsync(userId, adminUserId, KybDecisionAction.RequestInfo, note, last?.TrustScore ?? 0);
            await _unitOfWork.SaveChangesAsync();
            await TrySendRequestInfoEmailAsync(user, note);
            return Result.Success();
        }

        public async Task<Result> RejectUserAsync(Guid adminUserId, Guid userId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                return Result.Failure(AdminErrors.KybReasonRequired);

            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AdminErrors.UserNotFound);

            var note = reason.Trim();
            var last = await _analytics.GetLatestKybReportAsync(userId);
            var roles = await _userManager.GetRolesAsync(user);

            user.IsVerified = false;
            user.KybReviewStatus = KybReviewStatus.Rejected;
            user.KybAdminNote = note;
            user.KybReviewedAt = DateTime.UtcNow;
            user.KybReviewedByUserId = adminUserId;
            var updated = await _userManager.UpdateAsync(user);
            if (!updated.Succeeded)
            {
                return Result.Failure(AdminErrors.KybRejectFailed);
            }

            if (roles.Contains("Farm"))
            {
                var farm = await _farmRepository.GetByUserIdAsync(userId);
                if (farm is not null)
                {
                    farm.IsVerified = false;
                    _farmRepository.Update(farm);
                }
            }
            else if (roles.Contains("Factory"))
            {
                var factory = await _factoryRepository.GetByUserIdAsync(userId);
                if (factory is not null)
                {
                    factory.IsVerified = false;
                    _factoryRepository.Update(factory);
                }
            }

            try
            {
                await RecordDecisionAsync(userId, adminUserId, KybDecisionAction.Rejected, note, last?.TrustScore ?? 0);
                await _unitOfWork.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"KYB reject audit failed: {ex.Message}");
                try
                {
                    await _unitOfWork.SaveChangesAsync();
                }
                catch (Exception saveEx)
                {
                    Console.WriteLine($"KYB reject save failed: {saveEx.Message}");
                }
            }

            await TrySendRejectionEmailAsync(user, note);
            return Result.Success();
        }

        public async Task<Result<FarmHygieneDto>> GetFarmHygieneAsync(Guid farmId)
        {
            var farm = await _farmRepository.GetByIdAsync(farmId);
            if (farm is null)
                return Result<FarmHygieneDto>.Failure(FarmErrors.FarmNotFound);

            var detailed = await _farmRepository.GetFarmWithDetailsAsync(farm.UserId) ?? farm;
            var now = DateTime.UtcNow;
            var missing = NileChain.Domain.Common.KybRequirements
                .MissingRequiredKinds(
                    (detailed.FarmDocuments ?? Array.Empty<FarmDocument>()).Select(d => d.KybKind),
                    NileChain.Domain.Common.KybRequirements.RequiredForFarmVerifyWarning)
                .Select(k => k.ToString())
                .ToList();

            return Result<FarmHygieneDto>.Success(new FarmHygieneDto
            {
                FarmId = farm.FarmId,
                FarmName = farm.Name,
                IsVerified = farm.IsVerified,
                KybIncomplete = missing.Count > 0,
                MissingKybKinds = missing,
                Documents = (detailed.FarmDocuments ?? Array.Empty<FarmDocument>())
                    .OrderByDescending(d => d.UploadedAt)
                    .Select(d => new FarmHygieneDocumentDto
                    {
                        DocumentId = d.FarmDocumentId,
                        FileName = d.FileName,
                        FileUrl = d.FileUrl,
                        KybKind = d.KybKind.ToString(),
                        UploadedAt = d.UploadedAt
                    })
                    .ToList(),
                Certifications = (detailed.FarmCertifications ?? Array.Empty<FarmCertification>())
                    .Select(c => new FarmHygieneCertDto
                    {
                        CertificationId = c.CertificationId,
                        Name = c.Certification?.Name ?? "Unknown",
                        IssuedAt = c.IssuedAt,
                        ExpiresAt = c.ExpiresAt,
                        AdminGranted = c.GrantedByAdminUserId is not null && c.GrantedByAdminUserId != Guid.Empty,
                        IsExpired = c.ExpiresAt is not null && c.ExpiresAt <= now
                    })
                    .ToList()
            });
        }

        public async Task<Result<FactoryHygieneDto>> GetFactoryHygieneAsync(Guid factoryId)
        {
            var factory = await _factoryRepository.GetByIdAsync(factoryId);
            if (factory is null)
                return Result<FactoryHygieneDto>.Failure(FactoryErrors.FactoryNotFound);

            var detailed = await _factoryRepository.GetFactoryWithDetailsAsync(factory.UserId) ?? factory;
            var missing = NileChain.Domain.Common.KybRequirements
                .MissingRequiredKinds(
                    (detailed.FactoryDocuments ?? Array.Empty<FactoryDocument>()).Select(d => d.KybKind),
                    NileChain.Domain.Common.KybRequirements.RequiredForFactoryVerify)
                .Select(k => k.ToString())
                .ToList();

            return Result<FactoryHygieneDto>.Success(new FactoryHygieneDto
            {
                FactoryId = factory.FactoryId,
                FactoryName = factory.Name,
                IsVerified = factory.IsVerified,
                KybIncomplete = missing.Count > 0,
                MissingKybKinds = missing,
                Documents = (detailed.FactoryDocuments ?? Array.Empty<FactoryDocument>())
                    .OrderByDescending(d => d.UploadedAt)
                    .Select(d => new FarmHygieneDocumentDto
                    {
                        DocumentId = d.FactoryDocumentId,
                        FileName = d.FileName,
                        FileUrl = d.FileUrl,
                        KybKind = d.KybKind.ToString(),
                        UploadedAt = d.UploadedAt
                    })
                    .ToList()
            });
        }

        public async Task<Result<AdminOpsBadgesDto>> GetOpsBadgesAsync(CancellationToken cancellationToken = default)
        {
            var pendingVerifications = await _analytics.CountUnverifiedUsersAsync(cancellationToken);
            var openDisputes = await _analytics.CountOpenDisputesAsync(cancellationToken);
            var pendingWithdrawals = await _analytics.CountPendingWithdrawalsAsync(cancellationToken);
            return Result<AdminOpsBadgesDto>.Success(new AdminOpsBadgesDto
            {
                PendingVerifications = pendingVerifications,
                OpenDisputes = openDisputes,
                PendingWithdrawals = pendingWithdrawals
            });
        }

        public async Task<Result> GrantFarmCertificationAsync(
            Guid adminUserId,
            Guid farmId,
            GrantFarmCertificationRequest request)
        {
            var farm = await _farmRepository.GetByIdAsync(farmId);
            if (farm is null)
                return Result.Failure(FarmErrors.FarmNotFound);

            var detailed = await _farmRepository.GetFarmWithDetailsAsync(farm.UserId);
            if (detailed is null)
                return Result.Failure(FarmErrors.FarmNotFound);

            var issuedAt = request.IssuedAt ?? DateTime.UtcNow;
            if (request.ExpiresAt is not null && request.ExpiresAt <= issuedAt)
                return Result.Failure(FarmErrors.InvalidCertificationDates);

            var certification = await _certificationRepository.GetByIdAsync(request.CertificationId);
            if (certification is null)
                return Result.Failure(FarmErrors.CertificationNotFound);

            var existing = detailed.FarmCertifications
                .FirstOrDefault(c => c.CertificationId == request.CertificationId);
            if (existing is not null)
            {
                existing.GrantedByAdminUserId = adminUserId;
                existing.IssuedAt = issuedAt;
                existing.ExpiresAt = request.ExpiresAt;
            }
            else
            {
                detailed.FarmCertifications.Add(new FarmCertification
                {
                    FarmId = detailed.FarmId,
                    CertificationId = request.CertificationId,
                    IssuedAt = issuedAt,
                    ExpiresAt = request.ExpiresAt,
                    GrantedByAdminUserId = adminUserId,
                    Certification = certification
                });
            }

            await _unitOfWork.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<Result> RevokeFarmCertificationAsync(Guid farmId, Guid certificationId)
        {
            var farm = await _farmRepository.GetByIdAsync(farmId);
            if (farm is null)
                return Result.Failure(FarmErrors.FarmNotFound);

            var detailed = await _farmRepository.GetFarmWithDetailsAsync(farm.UserId);
            if (detailed is null)
                return Result.Failure(FarmErrors.FarmNotFound);

            var link = detailed.FarmCertifications.FirstOrDefault(c => c.CertificationId == certificationId);
            if (link is null)
                return Result.Failure(FarmErrors.CertificationNotOnFarm);

            detailed.FarmCertifications.Remove(link);
            await _unitOfWork.SaveChangesAsync();
            return Result.Success();
        }

        public async Task<Result> BlockUserAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AdminErrors.UserNotFound);

            user.LockoutEnabled = true;
            await _userManager.UpdateAsync(user);

            var isBlocked = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
            if (isBlocked)
                return Result.Failure(AdminErrors.UserAlreadyBlocked);

            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            await RefreshTokenRevocation.RevokeAllActiveAsync(_refreshTokenRepository, user.Id);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result> UnblockUserAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AdminErrors.UserNotFound);

            var isBlocked = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;
            if (!isBlocked)
                return Result.Failure(AdminErrors.UserNotBlocked);

            await _userManager.SetLockoutEndDateAsync(user, null);
            return Result.Success();
        }

        public async Task<Result> DeactivateUserAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AdminErrors.UserNotFound);

            if (!user.IsActive)
                return Result.Failure(AdminErrors.UserAlreadyDeactivated);

            user.IsActive = false;
            await _userManager.UpdateAsync(user);

            await RefreshTokenRevocation.RevokeAllActiveAsync(_refreshTokenRepository, user.Id);
            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }

        public async Task<Result> ReactivateUserAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AdminErrors.UserNotFound);

            if (user.IsActive)
                return Result.Failure(AdminErrors.UserNotDeactivated);

            user.IsActive = true;
            await _userManager.UpdateAsync(user);

            return Result.Success();
        }

        public Task<Result> DeleteUserAsync(Guid userId) =>
            _userAccountDeletionService.DeleteUserAccountAsync(userId);

        private async Task TrySendApprovalEmailAsync(ApplicationUser user)
        {
            if (_emailService is null || _templateRenderer is null || string.IsNullOrWhiteSpace(user.Email))
                return;

            try
            {
                var html = await _templateRenderer.RenderAsync(
                    EmailTemplates.KybApproved,
                    new Dictionary<string, string>
                    {
                        ["UserName"] = user.UserName ?? user.Email!,
                        ["LoginLink"] = $"{_appOptions.FrontendBaseUrl}/login"
                    });

                await _emailService.SendAsync(new EmailMessage
                {
                    To = user.Email!,
                    Subject = "Your NileChain account has been approved",
                    Body = html,
                    IsHtml = true
                });
            }
            catch
            {
                Console.WriteLine("Approval email failed");
            }
        }

        private async Task TrySendRejectionEmailAsync(ApplicationUser user, string reason)
        {
            if (_emailService is null || _templateRenderer is null || string.IsNullOrWhiteSpace(user.Email))
                return;

            try
            {
                var html = await _templateRenderer.RenderAsync(
                    EmailTemplates.KybRejected,
                    new Dictionary<string, string>
                    {
                        ["UserName"] = user.UserName ?? user.Email!,
                        ["SupportHint"] = reason
                    });

                await _emailService.SendAsync(new EmailMessage
                {
                    To = user.Email!,
                    Subject = "Your NileChain registration was not approved",
                    Body = html,
                    IsHtml = true
                });
            }
            catch
            {
                Console.WriteLine("Rejection email failed");
            }
        }

        private async Task TrySendRequestInfoEmailAsync(ApplicationUser user, string note)
        {
            if (_emailService is null || _templateRenderer is null || string.IsNullOrWhiteSpace(user.Email))
                return;

            try
            {
                var profilePath = (await _userManager.GetRolesAsync(user)).Contains("Factory")
                    ? "/factory/profile"
                    : "/farm/profile";
                var html = await _templateRenderer.RenderAsync(
                    EmailTemplates.KybRequestInfo,
                    new Dictionary<string, string>
                    {
                        ["UserName"] = user.UserName ?? user.Email!,
                        ["AdminNote"] = note,
                        ["ProfileLink"] = $"{_appOptions.FrontendBaseUrl}{profilePath}"
                    });

                await _emailService.SendAsync(new EmailMessage
                {
                    To = user.Email!,
                    Subject = "Please complete your NileChain documents",
                    Body = html,
                    IsHtml = true
                });
            }
            catch
            {
                Console.WriteLine("Request-info email failed");
            }
        }

        private async Task RecordDecisionAsync(
            Guid userId,
            Guid adminUserId,
            KybDecisionAction action,
            string reason,
            int trustScore)
        {
            if (_kybDecisions is null)
                return;

            await _kybDecisions.AddAsync(new KybDecision
            {
                DecisionId = Guid.NewGuid(),
                UserId = userId,
                AdminUserId = adminUserId,
                Action = action,
                Reason = string.IsNullOrWhiteSpace(reason) ? "—" : reason.Trim(),
                TrustScoreAtDecision = trustScore,
                CreatedAt = DateTime.UtcNow,
                User = null,
                AdminUser = null
            });
        }

        private static UserListItem MapUserListItem(
            ApplicationUser user,
            string roleName,
            bool isBlocked,
            LatestKybReportRow? lastReport,
            Subscription? latestPlan = null,
            DateTime? utcNow = null)
        {
            var now = utcNow ?? DateTime.UtcNow;
            var asFarm = string.Equals(roleName, "Farm", StringComparison.OrdinalIgnoreCase);
            var asFactory = string.Equals(roleName, "Factory", StringComparison.OrdinalIgnoreCase);
            string? planCode = null;
            string? planStatus = null;
            DateTime? periodEnd = null;
            if (asFarm || asFactory)
            {
                var live = latestPlan is not null && latestPlan.IsLiveAt(now)
                    && SubscriptionPlanCodes.IsPro(latestPlan.PlanCode);
                planCode = live ? latestPlan!.PlanCode : SubscriptionPlanCodes.FreeForRole(asFarm);
                planStatus = live ? latestPlan!.Status.ToString() : "Active";
                periodEnd = live ? latestPlan!.PeriodEnd : null;
            }

            return new()
            {
                Id = user.Id,
                Email = user.Email ?? "",
                DisplayName = user.Farm?.Name ?? user.Factory?.Name ?? user.UserName,
                Role = roleName,
                IsVerified = user.IsVerified,
                IsBlocked = isBlocked,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                FarmId = user.Farm?.FarmId,
                FactoryId = user.Factory?.FactoryId,
                FarmName = user.Farm?.Name,
                FactoryName = user.Factory?.Name,
                KybReviewStatus = user.KybReviewStatus.ToString(),
                KybAdminNote = user.KybAdminNote,
                LastTrustScore = lastReport?.TrustScore,
                LastRecommendation = lastReport?.Recommendation,
                PlanCode = planCode,
                SubscriptionStatus = planStatus,
                SubscriptionPeriodEnd = periodEnd
            };
        }

        private static VerifyUserResult MapReport(LatestKybReportRow row)
        {
            var comparison = new List<KybComparisonItemDto>();
            try
            {
                comparison = JsonSerializer.Deserialize<List<KybComparisonItemDto>>(
                    row.BreakdownJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            }
            catch
            {
                comparison = [];
            }

            var missing = comparison.Where(c => !c.Provided).Select(c => c.KybKind).ToList();
            return new VerifyUserResult
            {
                Verified = false,
                KybIncomplete = missing.Count > 0,
                MissingKybKinds = missing,
                TrustScore = row.TrustScore,
                OverallSummary = row.OverallSummary,
                Recommendation = row.Recommendation,
                Comparison = comparison
            };
        }

        public async Task<Result<RagUploadResult>> UploadRagDocumentAsync(
            Guid uploadedBy,
            string title,
            string? category,
            string filePath,
            string contentText)
        {
            if (string.IsNullOrWhiteSpace(title))
                return Result<RagUploadResult>.Failure(new Error("Admin.RagTitleRequired", "Document title is required."));

            var doc = new NileChain.Domain.Entities.RagDocument
            {
                DocumentId = Guid.NewGuid(),
                Title = title.Trim(),
                Category = string.IsNullOrWhiteSpace(category) ? "general" : category.Trim(),
                FilePath = filePath,
                UploadedBy = uploadedBy,
                UploadedAt = DateTime.UtcNow
            };

            await _ragDocumentRepository.AddAsync(doc);
            await _unitOfWork.SaveChangesAsync();

            var indexed = false;
            if (_ragIndexer is not null && !string.IsNullOrWhiteSpace(contentText))
            {
                indexed = await _ragIndexer.IndexDocumentAsync(
                    doc.DocumentId.ToString("N"),
                    doc.Title,
                    doc.Category,
                    contentText);
            }

            return Result<RagUploadResult>.Success(new RagUploadResult
            {
                DocumentId = doc.DocumentId,
                Title = doc.Title,
                Category = doc.Category,
                IndexedInChroma = indexed
            });
        }

        public async Task<Result<List<RagDocumentDto>>> GetRagDocumentsAsync()
        {
            var docs = await _ragDocumentRepository.GetAllAsync();
            var dtos = docs
                .OrderByDescending(d => d.UploadedAt)
                .Select(d => new RagDocumentDto
                {
                    DocumentId = d.DocumentId,
                    Title = d.Title,
                    Category = d.Category,
                    FilePath = d.FilePath,
                    UploadedAt = d.UploadedAt,
                    Status = string.IsNullOrWhiteSpace(d.FilePath) ? "pending" : "uploaded"
                })
                .ToList();

            return Result<List<RagDocumentDto>>.Success(dtos);
        }

        public async Task<Result<DashboardSummaryDto>> GetDashboardSummaryAsync(
            CancellationToken cancellationToken = default)
        {
            var asOf = DeliveryDatePolicy.ToUtcStorage(DateTime.UtcNow.Date);
            var fromUtc = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc)
                .AddMonths(-6);

            var pendingVerifications = await _analytics.CountUnverifiedUsersAsync(cancellationToken);
            var openDisputes = await _analytics.CountOpenDisputesAsync(cancellationToken);
            var stuck = await _analytics.CountStuckFulfillmentsAsync(asOf, cancellationToken);
            var pendingSig = await _analytics.CountContractsByStatusAsync(
                ContractStatus.PendingSignature, cancellationToken);
            var pendingFarm = await _analytics.CountContractsByStatusAsync(
                ContractStatus.PendingFarmSignature, cancellationToken);
            var pendingFactory = await _analytics.CountContractsByStatusAsync(
                ContractStatus.PendingFactorySignature, cancellationToken);
            var signed = await _analytics.CountContractsByStatusAsync(
                ContractStatus.Signed, cancellationToken);
            var farms = await _analytics.CountFarmsAsync(cancellationToken);
            var factories = await _analytics.CountFactoriesAsync(cancellationToken);
            var admins = await _analytics.CountUsersInRolesAsync(
                new[] { "Admin", "SuperAdmin" }, cancellationToken);
            var totalUsers = await _analytics.CountAllUsersAsync(cancellationToken);

            var monthlyRaw = await _analytics.GetMonthlyContractCountsAsync(fromUtc, cancellationToken);
            var monthKeys = Enumerable.Range(0, 7)
                .Select(i => fromUtc.AddMonths(i))
                .Select(d => (d.Year, d.Month, Label: d.ToString("MMM")))
                .ToList();
            var countByKey = monthlyRaw.ToDictionary(x => (x.Year, x.Month), x => x.Count);
            var maxCount = Math.Max(1, monthKeys.Max(m => countByKey.GetValueOrDefault((m.Year, m.Month))));
            var monthly = monthKeys.Select(m =>
            {
                var count = countByKey.GetValueOrDefault((m.Year, m.Month));
                return new MonthlyContractPointDto
                {
                    Label = m.Label,
                    Count = count,
                    HeightPercent = (int)Math.Round(100.0 * count / maxCount)
                };
            }).ToList();

            var cropsRaw = await _analytics.GetTopCropDemandAsync(5, cancellationToken);
            var crops = cropsRaw.Select(c =>
            {
                var risk = c.AvgRisk;
                var band = risk is null ? "medium"
                    : risk >= 70 ? "low"
                    : risk >= 40 ? "medium"
                    : "high";
                return new CropDemandDto
                {
                    CropName = c.CropName,
                    DemandTons = decimal.Round(c.DemandTons, 1, MidpointRounding.AwayFromZero),
                    AvgPricePerTon = c.AvgPrice is null
                        ? null
                        : decimal.Round(c.AvgPrice.Value, 0, MidpointRounding.AwayFromZero),
                    AvgRiskScore = risk is null
                        ? null
                        : decimal.Round(risk.Value, 0, MidpointRounding.AwayFromZero),
                    RiskBand = band
                };
            }).ToList();

            var activityRaw = await _analytics.GetRecentActivityAsync(8, cancellationToken);
            var activity = activityRaw.Select(a => new AdminActivityItemDto
            {
                Kind = a.Kind,
                Message = a.Message,
                OccurredAt = a.OccurredAt,
                Icon = a.Icon
            }).ToList();

            return Result<DashboardSummaryDto>.Success(new DashboardSummaryDto
            {
                PendingVerifications = pendingVerifications,
                OpenDisputes = openDisputes,
                StuckFulfillments = stuck,
                PendingSignatureContracts = pendingSig + pendingFarm + pendingFactory,
                SignedContracts = signed,
                FarmCount = farms,
                FactoryCount = factories,
                AdminCount = admins,
                TotalUsers = totalUsers,
                MonthlyContracts = monthly,
                TopCrops = crops,
                RecentActivity = activity
            });
        }

        public async Task<Result<AdminContractListDto>> GetContractsAsync(
            string? status,
            string? search,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            var (total, rows) = await _analytics.GetContractsAsync(
                status,
                search,
                (page - 1) * pageSize,
                pageSize,
                cancellationToken);

            var items = rows.Select(r =>
            {
                decimal? value = null;
                if (r.PricePerTon is > 0 && r.QuantityTons > 0)
                    value = decimal.Round(r.QuantityTons * r.PricePerTon.Value, 0, MidpointRounding.AwayFromZero);

                var risk = r.FarmRiskScore;
                var band = risk is null ? "medium"
                    : risk >= 70 ? "low"
                    : risk >= 40 ? "medium"
                    : "high";

                return new AdminContractListItemDto
                {
                    ContractId = r.ContractId,
                    ShortId = r.ContractId.ToString()[..8].ToUpperInvariant(),
                    FarmName = r.FarmName,
                    FactoryName = r.FactoryName,
                    CropName = r.QuantityTons > 0
                        ? $"{r.CropName} ({r.QuantityTons:0.##} ton)"
                        : r.CropName,
                    QuantityTons = r.QuantityTons,
                    ValueEgp = value,
                    FarmRiskScore = risk is null
                        ? null
                        : decimal.Round(risk.Value, 0, MidpointRounding.AwayFromZero),
                    RiskBand = band,
                    Status = MapAdminContractStatus(r.Status),
                    CreatedAt = r.CreatedAt
                };
            }).ToList();

            return Result<AdminContractListDto>.Success(new AdminContractListDto
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                Items = items
            });
        }

        private static string MapAdminContractStatus(ContractStatus status) => status switch
        {
            ContractStatus.Signed => "signed",
            ContractStatus.Cancelled => "rejected",
            ContractStatus.Draft => "review",
            _ => "review"
        };
    }
}
