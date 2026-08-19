using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Identity;
using NileChain.Application.Common;
using NileChain.Application.Errors;
using NileChain.Domain.Constants;
using NileChain.Domain.Identity;
using System.Security.Claims;

namespace NileChain.API.Filters;

/// <summary>
/// Marks an endpoint that unverified Farm/Factory users may call (profile, documents, catalogs).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AllowUnverifiedAttribute : Attribute, IFilterMetadata
{
}

/// <summary>
/// Blocks marketplace APIs for Farm/Factory JWTs until an admin approves KYB.
/// Profile, documents, and catalogs stay available.
/// </summary>
public sealed class RequireKybVerifiedFilter : IAsyncActionFilter
{
    private static readonly string[] AllowedPrefixes =
    [
        "/api/auth",
        "/api/crop-types",
        "/api/certifications",
        "/api/farm/profile",
        "/api/farm/documents",
        "/api/farm/crops",
        "/api/farm/images",
        "/api/farm/certifications",
        "/api/farm/notifications",
        "/api/factory/profile",
        "/api/factory/documents",
        "/api/factory/notifications"
    ];

    private readonly UserManager<ApplicationUser> _userManager;

    public RequireKybVerifiedFilter(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null
            || endpoint?.Metadata.GetMetadata<AllowUnverifiedAttribute>() is not null)
        {
            await next();
            return;
        }

        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        if (user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.SuperAdmin))
        {
            await next();
            return;
        }

        if (!user.IsInRole(AppRoles.Farm) && !user.IsInRole(AppRoles.Factory))
        {
            await next();
            return;
        }

        var path = context.HttpContext.Request.Path.Value ?? string.Empty;
        if (IsAllowedWhilePending(path))
        {
            await next();
            return;
        }

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            await next();
            return;
        }

        var appUser = await _userManager.FindByIdAsync(userId);
        if (appUser is null || appUser.IsVerified)
        {
            await next();
            return;
        }

        var error = AuthErrors.KybPending;
        context.Result = new ObjectResult(ResultHttpMapper.ToErrorBody(error))
        {
            StatusCode = StatusCodes.Status403Forbidden
        };
    }

    private static bool IsAllowedWhilePending(string path) =>
        AllowedPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
}
