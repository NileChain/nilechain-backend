using NileChain.Application.Common;

namespace NileChain.Application.Errors
{
    public static class AdminErrors
    {
        public static readonly Error UserNotFound =
            new("Admin.UserNotFound", "User not found.");

        public static readonly Error RoleNotFound =
            new("Admin.RoleNotFound", "The selected role does not exist.");

        public static readonly Error EmailAlreadyExists =
            new("Admin.EmailAlreadyExists", "An account with this email already exists.");

        public static readonly Error UserAlreadyVerified =
            new("Admin.UserAlreadyVerified", "User is already verified.");

        public static readonly Error UserAlreadyBlocked =
            new("Admin.UserAlreadyBlocked", "User is already blocked.");

        public static readonly Error UserNotBlocked =
            new("Admin.UserNotBlocked", "User is not blocked.");

        public static readonly Error UserAlreadyDeactivated =
            new("Admin.UserAlreadyDeactivated", "User is already deactivated.");

        public static readonly Error UserNotDeactivated =
            new("Admin.UserNotDeactivated", "User is not deactivated.");

        public static readonly Error CannotDeleteSelf =
            new("Admin.CannotDeleteSelf", "Admin cannot delete their own account.");

        public static readonly Error KybReasonRequired =
            new("Admin.KybReasonRequired", "A written reason is required for this KYB decision.");

        public static readonly Error KybRejectFailed =
            new("Admin.KybRejectFailed", "Could not save the KYB rejection.");

        public static readonly Error KybReportNotFound =
            new("Admin.KybReportNotFound", "No KYB analysis report was found for this user.");

        public static readonly Error KybUnsupportedRole =
            new("Admin.KybUnsupportedRole", "KYB review is only available for farm and factory accounts.");
    }
}
