using NileChain.Application.Common;

namespace NileChain.Application.Errors
{
    public static class AuthErrors
    {
        public static readonly Error EmailAlreadyExists =
            new(
                "Auth.EmailAlreadyExists",
                "An account with this email already exists.");

        public static readonly Error InvalidBusinessType =
            new(
                "Auth.InvalidBusinessType",
                "Business type must be Farm or Factory.");

        public static readonly Error RoleNotFound =
            new(
                "Auth.RoleNotFound",
                "The selected role does not exist.");

        public static readonly Error RegistrationFailed =
            new(
                "Auth.RegistrationFailed",
                "Registration failed.");

        public static readonly Error InvalidCredentials =
    new(
        "Auth.InvalidCredentials",
        "Invalid email or password.");

        public static readonly Error InvalidRefreshToken =
    new(
        "Auth.InvalidRefreshToken",
        "Refresh token is invalid.");

        public static readonly Error ExpiredRefreshToken =
            new(
                "Auth.ExpiredRefreshToken",
                "Refresh token has expired.");

        public static readonly Error RevokedRefreshToken =
            new(
                "Auth.RevokedRefreshToken",
                "Refresh token has been revoked.");

        public static readonly Error InvalidEmailConfirmationToken =
    new(
        "Auth.InvalidEmailConfirmationToken",
        "Invalid confirmation token.");

        public static readonly Error UserNotFound =
            new(
                "Auth.UserNotFound",
                "User not found.");

        public static readonly Error ResetPasswordFailed =
    new(
        "Auth.ResetPasswordFailed",
        "Failed to reset password.");

        public static readonly Error InvalidResetToken =
            new(
                "Auth.InvalidResetToken",
                "Invalid or malformed password reset token.");

        public static readonly Error PhoneUpdateFailed =
    new(
        "Auth.PhoneUpdateFailed",
        "Failed to update phone number.");

        public static readonly Error AccountLocked =
            new(
                "Auth.AccountLocked",
                "Your account has been blocked. Please contact support.");
    }
}
