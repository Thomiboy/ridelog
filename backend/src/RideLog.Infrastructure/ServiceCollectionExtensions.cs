using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideLog.Application.Auth;
using RideLog.Application.Import;
using RideLog.Application.Polar;
using RideLog.Application.Rides;
using RideLog.Application.Users;
using RideLog.Application.Weather;
using RideLog.Infrastructure.Auth;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Polar;
using RideLog.Infrastructure.Rides;
using RideLog.Infrastructure.Users;
using RideLog.Infrastructure.Weather;

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
        services.Configure<ExternalSignInOptions>(configuration.GetSection(ExternalSignInOptions.SectionName));

        services.AddIdentityCore<IdentityUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<RideLogDbContext>();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IExternalSignIn, ExternalSignIn>();
        // Singleton because a code issued by the callback is redeemed by a later, separate request.
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<ISignInCodes, SignInCodes>();
        services.AddHttpClient<IExternalProviders, ExternalProviders>();
        // The sign-in state crosses the redirect protected, exactly as the Polar link's does.
        services.AddDataProtection();
        services.AddScoped<RideLogInitializer>();

        return services;
    }

    /// <summary>Registers the GPX/TCX file parsers and the historical-import service.</summary>
    public static IServiceCollection AddRideLogImport(this IServiceCollection services)
    {
        services.AddScoped<IActivityFileParser, GpxActivityParser>();
        services.AddScoped<IActivityFileParser, TcxActivityParser>();
        services.AddScoped<IActivityFileParser, FitActivityParser>();
        services.AddScoped<IActivityImporter, ActivityImporter>();
        services.AddScoped<IRideMaintenanceService, RideMaintenanceService>();
        services.AddScoped<IUserSettingsService, UserSettingsService>();

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

    /// <summary>
    /// Registers historical weather enrichment: the Open-Meteo archive client and the top-up that
    /// fills rides in a batch at a time. Kept out of <see cref="AddRideLogPolar"/> on purpose —
    /// weather is fetched after a ride is committed, never inside the import (docs/adr/0005).
    /// </summary>
    public static IServiceCollection AddRideLogWeather(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IWeatherTopUpService, WeatherTopUpService>();
        services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(client =>
        {
            client.BaseAddress = new Uri("https://archive-api.open-meteo.com/");
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        return services;
    }
}
