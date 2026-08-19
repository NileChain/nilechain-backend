using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using NileChain.Application.Common;
using NileChain.Application.Dtos.Email;
using NileChain.Application.Dtos.Signing;
using NileChain.Application.Errors;
using NileChain.Application.Interfaces;
using NileChain.Domain.Common;
using NileChain.Domain.Entities;
using NileChain.Domain.Enums;
using NileChain.Domain.Identity;
using NileChain.Domain.Interfaces;

namespace NileChain.Application.Services;


public class SigningOtpService : ISigningOtpService
{
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    private readonly ISigningOtpRepository _otps;
    private readonly IContractSignatureRepository _signatures;
    private readonly IContractHashService _hash;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _email;
    private readonly ISigningOtpRateLimiter _rateLimiter;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ILogger<SigningOtpService> _logger;

    public SigningOtpService(
        ISigningOtpRepository otps,
        IContractSignatureRepository signatures,
        IContractHashService hash,
        IUnitOfWork unitOfWork,
        IEmailService email,
        ISigningOtpRateLimiter rateLimiter,
        UserManager<ApplicationUser> users,
        ILogger<SigningOtpService> logger)
    {
        _otps = otps;
        _signatures = signatures;
        _hash = hash;
        _unitOfWork = unitOfWork;
        _email = email;
        _rateLimiter = rateLimiter;
        _users = users;
        _logger = logger;
    }

    public async Task<Result<SigningOtpResponse>> SendOtpAsync(
        Guid contractId,
        Guid userId,
        string? ipAddress)
    {
        if (!_rateLimiter.TryAcquire(userId))
            return Result<SigningOtpResponse>.Failure(SigningOtpErrors.RateLimited);

        var contract = await _otps.GetContractForPartyAsync(contractId, userId);
        if (contract is null)
            return Result<SigningOtpResponse>.Failure(SigningOtpErrors.ContractNotFound);

        var user = await _users.FindByIdAsync(userId.ToString());
        if (user is null || string.IsNullOrWhiteSpace(user.Email))
            return Result<SigningOtpResponse>.Failure(SigningOtpErrors.EmailMissing);

        var otp = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var otpHash = SigningOtpHasher.Hash(otp);
        var now = DateTime.UtcNow;
        var expiresAt = now.Add(Ttl);

        var unused = await _otps.GetUnusedForUserContractAsync(userId, contractId);
        foreach (var previous in unused)
            previous.IsUsed = true;

        var entity = new SigningOtp
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            UserId = userId,
            OtpHash = otpHash,
            ExpiresAt = expiresAt,
            IsUsed = false,
            CreatedAt = now
        };
        await _otps.AddAsync(entity);

        var title = ContractTitle(contract);
        var contentHash = SafeHash(contract);
        var previousAudit = await _signatures.GetLatestAuditAsync(contractId);
        await _signatures.AddAuditAsync(new ContractAuditLog
        {
            Id = Guid.NewGuid(),
            ContractId = contractId,
            Action = ContractAuditAction.OtpIssued,
            ActorId = userId,
            IpAddress = ipAddress,
            Timestamp = now,
            StateHash = ContractAuditHasher.Compute(
                previousAudit?.StateHash,
                ContractAuditAction.OtpIssued,
                userId,
                now,
                contentHash,
                ipAddress)
        });

        await _unitOfWork.SaveChangesAsync();

        try
        {
            await _email.SendAsync(new EmailMessage
            {
                To = user.Email,
                Subject = "NileChain — كود تأكيد التوقيع",
                Body = BuildOtpBody(title, otp),
                IsHtml = true
            });
        }
        catch (Exception ex)
        {
            entity.IsUsed = true;
            await _unitOfWork.SaveChangesAsync();
            _logger.LogWarning(ex, "Failed to send signing OTP email for contract {ContractId}", contractId);
            return Result<SigningOtpResponse>.Failure(SigningOtpErrors.EmailFailed);
        }

        _logger.LogInformation(
            "Signing OTP issued for contract {ContractId} user {UserId}",
            contractId,
            userId);

        return Result<SigningOtpResponse>.Success(new SigningOtpResponse { ExpiresAt = expiresAt });
    }

    public async Task<Result> VerifyAndConsumeAsync(Guid contractId, Guid userId, string? otpCode)
    {
        if (string.IsNullOrWhiteSpace(otpCode) || otpCode.Trim().Length != 6)
            return Result.Failure(SigningOtpErrors.Invalid);

        var latest = await _otps.GetLatestUnusedAsync(userId, contractId);
        if (latest is null
            || latest.ExpiresAt <= DateTime.UtcNow
            || !SigningOtpHasher.Matches(latest.OtpHash, otpCode))
        {
            return Result.Failure(SigningOtpErrors.Invalid);
        }

        latest.IsUsed = true;
        return Result.Success();
    }

    private string SafeHash(Contract contract)
    {
        try
        {
            return _hash.ComputeHash(contract);
        }
        catch
        {
            return "";
        }
    }

    private static string ContractTitle(Contract contract)
    {
        var crop = contract.FarmMatch?.SupplyRequest?.CropType?.Name ?? "عقد";
        var farm = contract.FarmMatch?.Farm?.Name ?? "";
        var factory = contract.FarmMatch?.SupplyRequest?.Factory?.Name ?? "";
        return $"{crop} — {farm} / {factory}".Trim(' ', '—', '/');
    }

    private static string BuildOtpBody(string title, string otp)
    {
        var enc = HtmlEncoder.Default;
        return
            "<div dir=\"rtl\" style=\"font-family:Arial,sans-serif\">" +
            "<p>كود تأكيد التوقيع لعقد <b>" + enc.Encode(title) + "</b>:</p>" +
            "<p style=\"font-size:24px;letter-spacing:6px;font-weight:bold\">" + enc.Encode(otp) + "</p>" +
            "<p>ينتهي هذا الكود خلال 10 دقائق. لا تشارك الكود مع أي شخص.</p>" +
            "</div>";
    }
}
