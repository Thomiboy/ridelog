using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Settings;
using RideLog.Infrastructure.Persistence;
using RideLog.Infrastructure.Settings;

namespace RideLog.UnitTests.Persistence;

/// <summary>
/// The durability seam #166 could not reach. Its tests set the public log and read it back through
/// one process's <c>IOptions</c> singleton, so nothing there could have seen that the setting was
/// never stored anywhere. This writes with one store and reads with a <em>separate</em> one over the
/// same database — which is what the next process is — so a value that only ever lived in memory
/// would be lost here.
/// </summary>
public sealed class SettingsStorePersistenceTests : IDisposable
{
    private const string Key = "public-log-rider-id";

    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<RideLogDbContext> _options;

    public SettingsStorePersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _options = new DbContextOptionsBuilder<RideLogDbContext>().UseSqlite(_connection).Options;
        using var context = new RideLogDbContext(_options);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task A_separate_reader_sees_what_was_stored()
    {
        await using (var writeContext = new RideLogDbContext(_options))
        {
            await new SettingsStore(writeContext).SetAsync(Key, "rider-7");
        }

        await using (var readContext = new RideLogDbContext(_options))
        {
            var seen = await new SettingsStore(readContext).GetAsync(Key);
            Assert.Equal("rider-7", seen);
        }
    }

    [Fact]
    public async Task Storing_a_key_again_moves_it_rather_than_duplicating()
    {
        await using (var context = new RideLogDbContext(_options))
        {
            var store = new SettingsStore(context);
            await store.SetAsync(Key, "rider-7");
            await store.SetAsync(Key, "rider-9");
        }

        await using (var readContext = new RideLogDbContext(_options))
        {
            Assert.Equal("rider-9", await new SettingsStore(readContext).GetAsync(Key));
        }
    }

    [Fact]
    public async Task An_unset_key_reads_back_as_null()
    {
        await using var context = new RideLogDbContext(_options);
        Assert.Null(await new SettingsStore(context).GetAsync("never-written"));
    }
}
