using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Application.Auth;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// #172: moving the public log has to survive a restart. The two hosts here share one database, so
/// the second is the next process — it reads what the first stored, not what configuration said when
/// it booted. Configuration still names "admin-1"; the store names the rider the owner moved it to,
/// and the store has to win, or a signed-out visitor is served the wrong log after every cold start.
/// </summary>
public sealed class PublicLogRestartTests : IDisposable
{
    private sealed class RestartedOn(SqliteConnection connection)
        : RideLogApiFactory(connection, ownsConnection: false);

    // The test owns the connection: it is the database both boots share, and it outlives each host.
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public PublicLogRestartTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);
    private sealed record PagedDto(int Total);

    [Fact]
    public async Task A_moved_public_log_still_holds_after_a_restart()
    {
        string riderId;
        using (var firstBoot = new RestartedOn(_connection))
        {
            riderId = await GivenApprovedRiderWithARideAsync(firstBoot);
            var admin = await AdminClientAsync(firstBoot);
            var moved = await admin.PutAsJsonAsync("/riders/public-log", new { riderId });
            moved.EnsureSuccessStatusCode();
        }

        // The next process: a fresh host over the same database. Configuration still says "admin-1",
        // which has no rides here — so a visitor seeing the one ride can only be the stored rider.
        using var secondBoot = new RestartedOn(_connection);
        var seen = await secondBoot.CreateClient().GetFromJsonAsync<PagedDto>("/rides");

        Assert.Equal(1, seen!.Total);
    }

    private static async Task<HttpClient> AdminClientAsync(RideLogApiFactory factory)
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, RideLogApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<string> GivenApprovedRiderWithARideAsync(RideLogApiFactory factory)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Rider>>();
        var rider = new Rider
        {
            UserName = "restart@example.test",
            Email = "restart@example.test",
            EmailConfirmed = true,
            Approval = Approval.Approved,
        };
        await users.CreateAsync(rider);

        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        var start = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero);
        context.Rides.Add(new Ride
        {
            Id = Guid.NewGuid(),
            UserId = rider.Id,
            StartTime = start,
            EndTime = start.AddHours(1),
            Duration = TimeSpan.FromHours(1),
            DistanceMeters = 20_000,
            Sport = "ROAD_CYCLING",
            Source = RideSource.Polar,
        });
        await context.SaveChangesAsync();
        return rider.Id;
    }
}
