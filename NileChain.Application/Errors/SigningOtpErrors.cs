using NileChain.Application.Common;

namespace NileChain.Application.Errors;

public static class SigningOtpErrors
{
    public static readonly Error Invalid = new(
        "SigningOtp.Invalid",
        "كود التحقق غير صحيح أو منتهي الصلاحية");

    public static readonly Error RateLimited = new(
        "SigningOtp.RateLimited",
        "Too many verification-code requests. Please try again shortly.");

    public static readonly Error ContractNotFound = new(
        "SigningOtp.ContractNotFound",
        "Contract not found.");

    public static readonly Error EmailMissing = new(
        "SigningOtp.EmailMissing",
        "No email is registered for this account.");

    public static readonly Error EmailFailed = new(
        "SigningOtp.EmailFailed",
        "The verification code could not be sent. Please try again.");
}

public static class SignatureErrors
{
    public static readonly Error ContractNotFound = new(
        "Signature.ContractNotFound",
        "Contract not found.");

    public static readonly Error Forbidden = new(
        "Signature.Forbidden",
        "You do not have access to this contract.");

    public static readonly Error HmacNotConfigured = new(
        "Signature.HmacNotConfigured",
        "Signing HMAC secret is not configured.");
}
