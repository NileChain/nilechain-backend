using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NileChain.Application.Admin;
using NileChain.Application.Auth;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services
{
    public class AdminService : IAdminService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly IFarmRepository _farmRepository;
        private readonly IFactoryRepository _factoryRepository;
        private readonly IRepository<NileChain.Domain.Entities.RagDocument> _ragDocumentRepository;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IAdminAnalyticsRepository _analytics;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRagIndexer? _ragIndexer;

        public AdminService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IFarmRepository farmRepository,
            IFactoryRepository factoryRepository,
            IRepository<NileChain.Domain.Entities.RagDocument> ragDocumentRepository,
            IRefreshTokenRepository refreshTokenRepository,
            IAdminAnalyticsRepository analytics,
            IUnitOfWork unitOfWork,
            IRagIndexer? ragIndexer = null)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _farmRepository = farmRepository;
            _factoryRepository = factoryRepository;
            _ragDocumentRepository = ragDocumentRepository;
            _refreshTokenRepository = refreshTokenRepository;
            _analytics = analytics;
            _unitOfWork = unitOfWork;
            _ragIndexer = ragIndexer;
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

            if (isVerified.HasValue)
            {
                query = query.Where(u => u.IsVerified == isVerified.Value);
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
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var roleName = roles.FirstOrDefault() ?? "User";
                var isBlocked = user.LockoutEnabled && user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

                items.Add(new UserListItem
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    DisplayName = user.Farm?.Name ?? user.Factory?.Name ?? user.UserName,
                    Role = roleName,
                    IsVerified = user.IsVerified,
                    IsBlocked = isBlocked,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt,
                    FarmName = user.Farm?.Name,
                    FactoryName = user.Factory?.Name
                });
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
                IsVerified = role == "Admin"
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
                FactoryName = role == "Factory" ? request.Name : null
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
                FarmName = farmEnt?.Name,
                FactoryName = factoryEnt?.Name
            };
        }

        public async Task<Result> VerifyUserAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AdminErrors.UserNotFound);

            if (user.IsVerified)
                return Result.Failure(AdminErrors.UserAlreadyVerified);

            user.IsVerified = true;
            await _userManager.UpdateAsync(user);

            var roles = await _userManager.GetRolesAsync(user);

            if (roles.Contains("Farm"))
            {
                var farm = await _farmRepository.GetByUserIdAsync(userId);
                if (farm is not null)
                {
                    farm.IsVerified = true;
                    _farmRepository.Update(farm);
                    await _unitOfWork.SaveChangesAsync();
                }
            }
            else if (roles.Contains("Factory"))
            {
                var factory = await _factoryRepository.GetByUserIdAsync(userId);
                if (factory is not null)
                {
                    factory.IsVerified = true;
                    _factoryRepository.Update(factory);
                    await _unitOfWork.SaveChangesAsync();
                }
            }

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
