using NileChain.Domain.Constants;
using NileChain.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace NileChain.Infrastructure.Persistence;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        RoleManager<ApplicationRole> roleManager,
        UserManager<ApplicationUser> userManager)
    {
        foreach (var role in new[] { AppRoles.Farm, AppRoles.Factory, AppRoles.Admin })
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new ApplicationRole { Name = role });
        }

        const string adminEmail = "admin@gmail.com";
        if (await userManager.FindByEmailAsync(adminEmail) is null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                IsVerified = true,
                EmailConfirmed = true
            };
            await userManager.CreateAsync(admin, "Admin123@");
            await userManager.AddToRoleAsync(admin, AppRoles.Admin);
        }
    }
}
