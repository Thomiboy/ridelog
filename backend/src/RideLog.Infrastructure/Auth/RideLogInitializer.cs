using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Auth;

/// <summary>Prepares the database schema and seeds the admin role and user on startup.</summary>
public sealed class RideLogInitializer(
    RideLogDbContext context,
    UserManager<IdentityUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<AdminSeedOptions> adminOptions)
{
    private readonly AdminSeedOptions _admin = adminOptions.Value;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        // SQL Server (dev/prod) applies migrations; other providers (SQLite in tests) build from the model.
        if (context.Database.ProviderName?.Contains("SqlServer", StringComparison.Ordinal) == true)
        {
            await context.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await context.Database.EnsureCreatedAsync(cancellationToken);
        }

        await SeedAdminAsync();
    }

    private async Task SeedAdminAsync()
    {
        if (string.IsNullOrWhiteSpace(_admin.Email) || string.IsNullOrWhiteSpace(_admin.Password))
        {
            return;
        }

        if (!await roleManager.RoleExistsAsync(AdminSeedOptions.RoleName))
        {
            await roleManager.CreateAsync(new IdentityRole(AdminSeedOptions.RoleName));
        }

        var admin = await userManager.FindByEmailAsync(_admin.Email);
        if (admin is null)
        {
            admin = new IdentityUser
            {
                UserName = _admin.Email,
                Email = _admin.Email,
                EmailConfirmed = true,
            };
            var result = await userManager.CreateAsync(admin, _admin.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"Failed to seed the admin user: {errors}");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, AdminSeedOptions.RoleName))
        {
            await userManager.AddToRoleAsync(admin, AdminSeedOptions.RoleName);
        }
    }
}
