using NileChain.Domain.Constants;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NileChain.Infrastructure.Persistence;

public static class IdentitySeeder
{
    public const string DefaultAdminEmail = "admin@gmail.com";
    private const string DefaultAdminPassword = "Admin123@";

    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IHostEnvironment environment,
        IConfiguration configuration,
        ILogger? logger = null)
    {
        foreach (var role in new[]
                 {
                     AppRoles.Farm,
                     AppRoles.Factory,
                     AppRoles.Admin,
                     AppRoles.SuperAdmin
                 })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
        }

        if (environment.IsProduction())
        {
            await SeedConfiguredAdminAsync(userManager, configuration, logger);
            return;
        }

        // Development / non-Production: preserve existing default admin bootstrap.
        if (await userManager.FindByEmailAsync(DefaultAdminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = DefaultAdminEmail,
                Email = DefaultAdminEmail,
                IsVerified = true,
                IsActive = true,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, DefaultAdminPassword);
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
            logger?.LogInformation("Default development admin user ensured.");
        }
    }

    /// <summary>
    /// Production: create an admin only when IdentitySeed:AdminEmail and IdentitySeed:AdminPassword are set.
    /// Never uses the known development password.
    /// </summary>
    private static async Task SeedConfiguredAdminAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        ILogger? logger)
    {
        var email = configuration["IdentitySeed:AdminEmail"];
        var password = configuration["IdentitySeed:AdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger?.LogInformation(
                "Skipping production admin bootstrap (set IdentitySeed__AdminEmail and IdentitySeed__AdminPassword to create one).");
            return;
        }

        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            IsVerified = true,
            IsActive = true,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            logger?.LogError("Production admin bootstrap failed: {Errors}", errors);
            throw new InvalidOperationException("Production admin bootstrap failed. Check IdentitySeed credentials meet password rules.");
        }

        await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        logger?.LogInformation("Production admin user created from IdentitySeed configuration.");
    }
}
