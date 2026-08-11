namespace NileChain.Application.Auth;

/// <summary>
/// Strict allowlist for Admin create/update user role assignment.
/// SuperAdmin must be provisioned out-of-band (seed/bootstrap), never via Admin APIs.
/// </summary>
public static class AdminRoleAllowlist
{
    public static string NormalizeOrThrow(string? role)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new InvalidOperationException("Role is required.");

        return role.Trim().ToLowerInvariant() switch
        {
            "farm" => "Farm",
            "factory" => "Factory",
            "admin" => "Admin",
            "superadmin" => throw new InvalidOperationException(
                "SuperAdmin cannot be assigned through admin APIs."),
            _ => throw new InvalidOperationException(
                $"Role '{role.Trim()}' is not allowed. Allowed roles: Farm, Factory, Admin.")
        };
    }
}
