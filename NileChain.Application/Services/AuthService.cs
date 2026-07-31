using NileChain.Application.Common;
using NileChain.Application.Dtos.Auth.Requests;
using NileChain.Application.Dtos.Auth.Responses;
using NileChain.Application.Dtos.Email;
using NileChain.Application.Email;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Entities;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Text;

namespace NileChain.Application.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ITokenService _tokenService;
        private readonly IRefreshTokenRepository _refreshTokenRepository;
        private readonly IEmailService _emailService;
        private readonly ITemplateRenderer _templateRenderer;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IFarmService _farmService;
        private readonly IFactoryService _factoryService;
        private readonly AppOptions _appOptions;

        public AuthService(
             UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ITokenService tokenService,
            IRefreshTokenRepository refreshTokenRepository,
            IUnitOfWork unitOfWork,
            IEmailService emailService,
            ITemplateRenderer templateRenderer,
            IFarmService farmService,
            IFactoryService factoryService,
            IOptions<AppOptions> appOptions)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
            _refreshTokenRepository = refreshTokenRepository;
            _unitOfWork = unitOfWork;
            _emailService = emailService;
            _templateRenderer = templateRenderer;
            _farmService = farmService;
            _factoryService = factoryService;
            _appOptions = appOptions.Value;
        }

        public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request)
        {
            // Check Email
            var existingUser = await _userManager.FindByEmailAsync(request.Email);

            if (existingUser is not null)
                return Result<AuthResponse>.Failure(AuthErrors.EmailAlreadyExists);

            // Check Role
            var role = request.BusinessType.Trim().ToLower() switch
            {
                "farm" => "Farm",
                "factory" => "Factory",
                _ => request.BusinessType
            };

            if (!await _roleManager.RoleExistsAsync(role))
                return Result<AuthResponse>.Failure(AuthErrors.RoleNotFound);

            // Create User
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = request.Email,
                Email = request.Email,
                CreatedAt = DateTime.UtcNow,
                IsVerified = false
            };

            var createResult = await _userManager.CreateAsync(user, request.Password);

            if (!createResult.Succeeded)
            {
                var errors = string.Join(
                    Environment.NewLine,
                    createResult.Errors.Select(e => e.Description));

                return Result<AuthResponse>.Failure(
                    new Error(
                        "Auth.RegistrationFailed",
                        errors));
            }

            // Add Role
            var roleResult = await _userManager.AddToRoleAsync(user, role);

            if (!roleResult.Succeeded)
            {
                await _userManager.DeleteAsync(user);

                return Result<AuthResponse>.Failure(
                    new Error(
                        "Auth.RoleAssignmentFailed",
                        roleResult.Errors.First().Description));
            }

            if (role == "Farm")
            {
                if (string.IsNullOrWhiteSpace(request.Name)
                    || string.IsNullOrWhiteSpace(request.Governorate)
                    || request.SizeInFeddans is not decimal sizeInFeddans
                    || sizeInFeddans <= 0)
                {
                    await _userManager.DeleteAsync(user);
                    return Result<AuthResponse>.Failure(AuthErrors.RegistrationFailed);
                }

                await _farmService.RegisterFarmAsync(
                    user.Id,
                    request.Name,
                    request.Governorate,
                    sizeInFeddans);
            }
            else if (role == "Factory")
            {
                if (string.IsNullOrWhiteSpace(request.Name)
                    || string.IsNullOrWhiteSpace(request.Governorate))
                {
                    await _userManager.DeleteAsync(user);
                    return Result<AuthResponse>.Failure(AuthErrors.RegistrationFailed);
                }

                await _factoryService.RegisterFactoryAsync(
                    user.Id,
                    request.Name,
                    request.Governorate);
            }

            var token =
                await _userManager.GenerateEmailConfirmationTokenAsync(user);

            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(token));

            var confirmationLink =
    $"{_appOptions.FrontendBaseUrl}/confirm-email?userId={user.Id}&token={encodedToken}";

            var html = await _templateRenderer.RenderAsync(
                    EmailTemplates.ConfirmEmail,
                    new Dictionary<string, string>
                    {
                        ["ConfirmationLink"] = confirmationLink
                    });
            try
            {
                await _emailService.SendAsync(new EmailMessage
                {
                    To = user.Email!,
                    Subject = "Confirm your email",
                    Body = html,
                    IsHtml = true
                });
            }
            catch
            {
                Console.WriteLine("Gmail is Down");
            }

            // Get Roles
            var roles = await _userManager.GetRolesAsync(user);

            // Access Token
            var accessToken = _tokenService.GenerateAccessToken(user, roles);

            // Revoke old active refresh tokens (Refresh Token Rotation)
            var activeTokens =
                await _refreshTokenRepository
                    .GetActiveTokensByUserIdAsync(user.Id);

            foreach (var token1 in activeTokens)
            {
                await _refreshTokenRepository.RevokeAsync(token1);
            }

            await _unitOfWork.SaveChangesAsync();

            // Generate new refresh token
            var refreshToken = _tokenService.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = _tokenService.HashRefreshToken(refreshToken.Token),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = refreshToken.ExpiresAt
            };

            await _refreshTokenRepository.AddAsync(refreshTokenEntity);

            await _unitOfWork.SaveChangesAsync();

            var response = new AuthResponse
            {
                AccessToken = accessToken.Token,
                RefreshToken = refreshToken.Token,
                ExpiresAt = accessToken.ExpiresAt,

                User = new UserResponse
                {
                    Id = user.Id,
                    Email = user.Email!,
                    Role = roles.First(),
                    EmailConfirmed = user.EmailConfirmed,
                    IsVerified = user.IsVerified
                }
            };

            return Result<AuthResponse>.Success(response);
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest request)
        {
            var user = await _userManager.FindByEmailAsync(request.Email);

            if (user is null)
                return Result<AuthResponse>.Failure(AuthErrors.InvalidCredentials);

            if (await _userManager.IsLockedOutAsync(user))
                return Result<AuthResponse>.Failure(AuthErrors.AccountLocked);

            var isPasswordValid =
                await _userManager.CheckPasswordAsync(user, request.Password);

            if (!isPasswordValid)
                return Result<AuthResponse>.Failure(AuthErrors.InvalidCredentials);

            var roles = await _userManager.GetRolesAsync(user);

            var accessToken =
                _tokenService.GenerateAccessToken(user, roles);

            // Revoke old active refresh tokens (Refresh Token Rotation)
            var activeTokens =
                await _refreshTokenRepository
                    .GetActiveTokensByUserIdAsync(user.Id);

            foreach (var token in activeTokens)
            {
                await _refreshTokenRepository.RevokeAsync(token);
            }

            await _unitOfWork.SaveChangesAsync();

            // Generate new refresh token
            var refreshToken =
                _tokenService.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash =
                    _tokenService.HashRefreshToken(refreshToken.Token),

                CreatedAt = DateTime.UtcNow,

                ExpiresAt = refreshToken.ExpiresAt
            };

            await _refreshTokenRepository.AddAsync(refreshTokenEntity);

            await _unitOfWork.SaveChangesAsync();

            return Result<AuthResponse>.Success(
                new AuthResponse
                {
                    AccessToken = accessToken.Token,

                    RefreshToken = refreshToken.Token,

                    ExpiresAt = accessToken.ExpiresAt,

                    User = new UserResponse
                    {
                        Id = user.Id,

                        Email = user.Email!,

                        Role = roles.Single(),

                        EmailConfirmed = user.EmailConfirmed,

                        IsVerified = user.IsVerified
                    }
                });
        }

        public async Task<Result<AuthResponse>> RefreshTokenAsync(
    RefreshTokenRequest request)
        {
            var hash =
                _tokenService.HashRefreshToken(request.RefreshToken);

            var refreshToken =
                await _refreshTokenRepository.GetByTokenHashAsync(hash);

            if (refreshToken is null)
                return Result<AuthResponse>.Failure(
                    AuthErrors.InvalidRefreshToken);

            if (refreshToken.IsRevoked)
                return Result<AuthResponse>.Failure(
                    AuthErrors.RevokedRefreshToken);

            if (refreshToken.IsExpired)
                return Result<AuthResponse>.Failure(
                    AuthErrors.ExpiredRefreshToken);

            var user = refreshToken.User;

            var roles =
                await _userManager.GetRolesAsync(user);

            var accessToken =
                _tokenService.GenerateAccessToken(user, roles);

            var newRefreshToken =
                _tokenService.GenerateRefreshToken();

            refreshToken.RevokedAt = DateTime.UtcNow;

            var refreshEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),

                UserId = user.Id,

                TokenHash =
                    _tokenService.HashRefreshToken(
                        newRefreshToken.Token),

                CreatedAt = DateTime.UtcNow,

                ExpiresAt = newRefreshToken.ExpiresAt
            };

            await _refreshTokenRepository.AddAsync(refreshEntity);

            await _unitOfWork.SaveChangesAsync();

            return Result<AuthResponse>.Success(
                new AuthResponse
                {
                    AccessToken = accessToken.Token,

                    RefreshToken = newRefreshToken.Token,

                    ExpiresAt = accessToken.ExpiresAt,

                    User = new UserResponse
                    {
                        Id = user.Id,

                        Email = user.Email!,

                        Role = roles.Single(),

                        EmailConfirmed = user.EmailConfirmed,

                        IsVerified = user.IsVerified
                    }
                });
        }

        public async Task<Result<CurrentUserResponse>> GetCurrentUserAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());

            if (user is null)
            {
                return Result<CurrentUserResponse>.Failure(
                    new Error(
                        "Auth.UserNotFound",
                        "User not found."));
            }

            var roles = await _userManager.GetRolesAsync(user);

            return Result<CurrentUserResponse>.Success(
                new CurrentUserResponse
                {
                    Id = user.Id,
                    Email = user.Email!,
                    Role = roles.Single(),
                    EmailConfirmed = user.EmailConfirmed,
                    IsVerified = user.IsVerified
                });
        }

        public async Task<Result> ConfirmEmailAsync(Guid userId, string token)
        {
            var user =
                await _userManager.FindByIdAsync(userId.ToString());

            if (user is null)
                return Result.Failure(AuthErrors.UserNotFound);

            if (user.EmailConfirmed)
            {
                return Result.Success();
            }

            var decodedToken = Encoding.UTF8.GetString(
                WebEncoders.Base64UrlDecode(token));

            var result =
                 await _userManager.ConfirmEmailAsync(
                     user,
                     decodedToken);

            if (!result.Succeeded)
            {
                return Result.Failure(
                    AuthErrors.InvalidEmailConfirmationToken);
            }

            return Result.Success();
        }

        public async Task<Result> ForgotPasswordAsync(
    ForgotPasswordRequest request)
        {
            var user =
                await _userManager.FindByEmailAsync(request.Email);

            // Security
            // Don't reveal whether the email exists.

            if (user is null)
                return Result.Success();

            var token =
                await _userManager.GeneratePasswordResetTokenAsync(user);

            var encodedToken =
                WebEncoders.Base64UrlEncode(
                    Encoding.UTF8.GetBytes(token));

            var resetLink =
                $"{_appOptions.FrontendBaseUrl}/reset-password" +
                $"?email={user.Email}" +
                $"&token={encodedToken}";

            var html =
                await _templateRenderer.RenderAsync(
                    EmailTemplates.ResetPassword,
                    new Dictionary<string, string>
                    {
                        ["ResetLink"] = resetLink
                    });

            await _emailService.SendAsync(
                new EmailMessage
                {
                    To = user.Email!,
                    Subject = "Reset your NileChain password",
                    Body = html,
                    IsHtml = true
                });

            return Result.Success();
        }

        public async Task<Result> ResetPasswordAsync(
    ResetPasswordRequest request)
        {
            var user =
                await _userManager.FindByEmailAsync(request.Email);

            if (user is null)
                return Result.Failure(AuthErrors.UserNotFound);

            string decodedToken;
            try
            {
                decodedToken = Encoding.UTF8.GetString(
                    WebEncoders.Base64UrlDecode(request.Token));
            }
            catch (FormatException)
            {
                return Result.Failure(AuthErrors.InvalidResetToken);
            }

            var result =
                await _userManager.ResetPasswordAsync(
                    user,
                    decodedToken,
                    request.NewPassword);

            if (!result.Succeeded)
            {
                var errors = string.Join(
                    Environment.NewLine,
                    result.Errors.Select(e => e.Description));

                return Result.Failure(
                    new Error(
                        "Auth.ResetPasswordFailed",
                        errors));
            }

            return Result.Success();
        }

        public async Task<Result> UpdatePhoneAsync(Guid userId, string phoneNumber)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user is null)
                return Result.Failure(AuthErrors.UserNotFound);

            user.PhoneNumber = phoneNumber;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return Result.Failure(AuthErrors.PhoneUpdateFailed);

            return Result.Success();
        }

        public async Task<Result> LogoutAsync(string refreshToken)
        {
            var hash = _tokenService.HashRefreshToken(refreshToken);

            var token =
                await _refreshTokenRepository.GetByTokenHashAsync(hash);

            if (token is null)
                return Result.Failure(AuthErrors.InvalidRefreshToken);

            if (token.IsRevoked)
                return Result.Failure(AuthErrors.RevokedRefreshToken);

            await _refreshTokenRepository.RevokeAsync(token);

            await _unitOfWork.SaveChangesAsync();

            return Result.Success();
        }

    }
}
