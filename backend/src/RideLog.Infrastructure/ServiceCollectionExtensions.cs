using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RideLog.Application.Auth;
using RideLog.Application.Import;
using RideLog.Application.Polar;
using RideLog.Infrastructure.Auth;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Polar;

// Placed in the DI namespace so callers get the extensions without an extra using.
namespace Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRideLogPersistence(this IServiceCollection services, string connectionString)
        => services.AddDbContext<RideLogDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
                // Azure SQL free offer auto-pauses; the first connection after a cold start returns
                // "database not currently available" (40613) while it resumes. Retry transient
                // failures so startup migration and seeding wait it out instead of crashing.
                sql.EnableRetryOnFailure(maxRetryCount: 10, maxRetryDelay: TimeSpan.FromSeconds(30), errorNumbersToAdd: null)));

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

    /// <summary>Registers the GPX/TCX file parsers and the historical-import service.</summary>
    public static IServiceCollection AddRideLogImport(this IServiceCollection services)
    {
        services.AddScoped<IActivityFileParser, GpxActivityParser>();
        services.AddScoped<IActivityFileParser, TcxActivityParser>();
        services.AddScoped<IActivityImporter, ActivityImporter>();

        return services;
    }

    /// <summary>
    /// Registers the Polar AccessLink OAuth flow, API client, encrypted token store, and sync service.
    /// Depends on the GPX/TCX parsers from <see cref="AddRideLogImport"/>.
    /// </summary>
    public static IServiceCollection AddRideLogPolar(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PolarOptions>(configuration.GetSection(PolarOptions.SectionName));
        services.AddDataProtection();

        services.AddScoped<IPolarTokenStore, PolarTokenStore>();
        services.AddScoped<IPolarSyncService, PolarSyncService>();
        services.AddHttpClient<IPolarClient, PolarApiClient>();
        services.AddHttpClient<IPolarOAuth, PolarOAuthClient>();

        return services;
    }
}
