using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideLog.Infrastructure.Persistence;

namespace RideLog.UnitTests.Auth;

/// <summary>
/// Boots the real API pipeline for thin HTTP tests, swapping SQL Server for a shared
/// in-memory SQLite database and supplying JWT + seeded-admin configuration.
/// </summary>
public class RideLogApiFactory : WebApplicationFactory<Program>
{
    public const string AdminEmail = "admin@ridelog.test";
    public const string AdminPassword = "Str0ng!Passw0rd";
    public const string SyncSharedSecret = "cron-shared-secret";
    public const string OwnerEmail = "owner@ridelog.test";

    private readonly SqliteConnection _connection;
    private readonly bool _ownsConnection;

    public RideLogApiFactory()
        : this(new SqliteConnection("DataSource=:memory:"), ownsConnection: true)
    {
    }

    /// <summary>
    /// Shares one open database across hosts, so a second host is the next process rather than a new
    /// database: it reads what the first one stored. The caller owns the connection and its lifetime.
    /// </summary>
    protected RideLogApiFactory(SqliteConnection connection, bool ownsConnection)
    {
        _connection = connection;
        _ownsConnection = ownsConnection;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (_connection.State != System.Data.ConnectionState.Open)
        {
            _connection.Open();
        }

        builder.UseEnvironment("Testing");

        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:RideLog"] = "Data Source=ignored",
            ["Jwt:Issuer"] = "ridelog-api",
            ["Jwt:Audience"] = "ridelog-web",
            ["Jwt:SigningKey"] = "integration-test-signing-key-32-bytes!!",
            ["Jwt:TokenLifetimeMinutes"] = "60",
            // The rides these tests seed belong to "admin-1", so that is the public log here.
            ["PublicLog:RiderId"] = "admin-1",
            ["AdminUser:Email"] = AdminEmail,
            ["AdminUser:Password"] = AdminPassword,
            ["Cors:AllowedOrigins:0"] = "https://localhost:4200",
            ["Polar:SyncSharedSecret"] = SyncSharedSecret,
            ["Polar:ClientId"] = "test-client",
            ["Polar:RedirectUri"] = "https://localhost:7016/polar/callback",
            // The owner's mailbox — where contact mail lands, and the address the contact page shows
            // when the form is switched off. The real sender is faked in the contact tests.
            ["Mail:OwnerAddress"] = OwnerEmail,
            ["Mail:ApiKey"] = "test-mail-key",
            // A whole suite posts to /contact in one run; keep the frequency guard from tripping tests.
            ["Contact:RateLimitPerWindow"] = "1000",
        };
        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            var toRemove = services.Where(descriptor =>
                descriptor.ServiceType == typeof(DbContextOptions<RideLogDbContext>) ||
                descriptor.ServiceType == typeof(DbContextOptions) ||
                descriptor.ServiceType == typeof(RideLogDbContext) ||
                descriptor.ServiceType.Name.StartsWith("IDbContextOptionsConfiguration", StringComparison.Ordinal))
                .ToList();
            foreach (var descriptor in toRemove)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<RideLogDbContext>(options => options.UseSqlite(_connection));

            ConfigureExtraServices(services);
        });
    }

    /// <summary>Hook for subclasses to swap in test doubles (e.g. a fake Polar client).</summary>
    protected virtual void ConfigureExtraServices(IServiceCollection services)
    {
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsConnection)
        {
            _connection.Dispose();
        }
    }
}
