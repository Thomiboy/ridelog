using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RideLog.Application.Auth;
using RideLog.Infrastructure.Auth;
using RideLog.Infrastructure.Persistence;

// Placed in the DI namespace so callers get the extensions without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRideLogPersistence(this IServiceCollection services, string connectionString)
        => services.AddDbContext<RideLogDbContext>(options => options.UseSqlServer(connectionString));

    /// <summary>
    /// Registers ASP.NET Core Identity (stored in RideLogDbContext), JWT token issuing, the login
    /// service, and the startup initializer. Web-side bearer validation and authorization policies
    /// are configured in the API layer.
    /// </summary>
    public static IServiceCollection AddRideLogAuth(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));

        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<RideLogDbContext>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<RideLogInitializer>();

        return services;
    }
}
