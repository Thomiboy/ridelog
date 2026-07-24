using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Domain.Users;
using RideLog.Infrastructure.Persistence;

namespace RideLog.UnitTests.Persistence;

public sealed class UserSettingsPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public UserSettingsPersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task User_settings_round_trip_keyed_by_user()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            context.UserSettings.Add(new UserSettings { UserId = "admin-1", MaxHeartRate = 190 });
            await context.SaveChangesAsync();
        }

        await using (var context = new RideLogDbContext(_options))
        {
            var loaded = await context.UserSettings.SingleAsync(s => s.UserId == "admin-1");
            Assert.Equal(190, loaded.MaxHeartRate);
        }
    }
}
