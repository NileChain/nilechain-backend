using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NileChain.Application.Dtos.Admin;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IRagIndexer? _ragIndexer;

        public AdminService(
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            IFarmRepository farmRepository,
            IFactoryRepository factoryRepository,
            IRepository<NileChain.Domain.Entities.RagDocument> ragDocumentRepository,
            IUnitOfWork unitOfWork,
            IRagIndexer? ragIndexer = null)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _farmRepository = farmRepository;
            _factoryRepository = factoryRepository;
            _ragDocumentRepository = ragDocumentRepository;
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

            var role = request.Role.Trim().ToLower() switch
            {
                "farm" => "Farm",
                "factory" => "Factory",
                "admin" => "Admin",
                _ => request.Role
            };

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
                var newRole = request.Role.Trim().ToLower() switch
                {
                    "farm" => "Farm",
                    "factory" => "Factory",
                    "admin" => "Admin",
                    _ => request.Role
                };

                if (!await _roleManager.RoleExistsAsync(newRole))
                    throw new InvalidOperationException("The selected role does not exist.");

                await _userManager.RemoveFromRoleAsync(user, currentRole);

                if (currentRole == "Farm")
                {
                    var farm = await _farmRepository.GetByUserIdAsync(userId);
                    if (farm is not null)
                    {
                        _farmRepository.Remove(farm);
                        await _unitOfWork.SaveChangesAsync();
                    }
                }
                else if (currentRole == "Factory")
                {
                    var factory = await _factoryRepository.GetByUserIdAsync(userId);
                    if (factory is not null)
                    {
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
                    Status = "indexed"
                })
                .ToList();

            return Result<List<RagDocumentDto>>.Success(dtos);
        }
    }
}
