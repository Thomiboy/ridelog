using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Polar;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Polar;

namespace RideLog.UnitTests.Polar;

public sealed class PolarTokenStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;
    private readonly IDataProtectionProvider _protection = new EphemeralDataProtectionProvider();

    public PolarTokenStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private PolarTokenStore NewStore(RideLogDbContext context) => new(context, _protection);

    [Fact]
    public async Task Saved_token_round_trips()
    {
        var token = new PolarToken("access-abc-123", "polar-user-9");

        await using (var context = new RideLogDbContext(_options))
        {
            await NewStore(context).SaveAsync("admin-1", token);
        }

        await using (var context = new RideLogDbContext(_options))
        {
            var connection = await NewStore(context).GetConnectionAsync();

            Assert.NotNull(connection);
            Assert.Equal("admin-1", connection.AppUserId);
            Assert.Equal("access-abc-123", connection.Token.AccessToken);
            Assert.Equal("polar-user-9", connection.Token.PolarUserId);
        }
    }

    [Fact]
    public async Task Token_is_encrypted_at_rest()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            await NewStore(context).SaveAsync("admin-1", new PolarToken("super-secret-token", "polar-user-9"));
        }

        await using (var verify = new RideLogDbContext(_options))
        {
            var stored = await verify.Set<PolarConnection>().SingleAsync();
            Assert.DoesNotContain("super-secret-token", stored.AccessTokenProtected);
        }
    }

    [Fact]
    public async Task Re_linking_replaces_the_existing_connection()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            await NewStore(context).SaveAsync("admin-1", new PolarToken("first", "polar-user-9"));
        }
        await using (var context = new RideLogDbContext(_options))
        {
            await NewStore(context).SaveAsync("admin-1", new PolarToken("second", "polar-user-9"));
        }

        await using (var verify = new RideLogDbContext(_options))
        {
            Assert.Equal(1, await verify.Set<PolarConnection>().CountAsync());
            var connection = await NewStore(verify).GetConnectionAsync();
            Assert.Equal("second", connection!.Token.AccessToken);
        }
    }
}
