namespace RideLog.Infrastructure.Auth;

/// <summary>The single admin user seeded on startup, bound from the "AdminUser" configuration section.</summary>
public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminUser";

    public const string RoleName = "Admin";

    public string Email { get; set; } = string.Empty;

    /// <summary>Initial password; supplied via user-secrets/env, never committed.</summary>
    public string Password { get; set; } = string.Empty;
}
