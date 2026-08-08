using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.TestHost;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// The public log is a setting, and a setting nobody remembers to fill in is a blank site. Left
/// unset it falls back to the seeded admin — the rider whose log has always been the public one —
/// so deploying this cannot quietly empty the pages it was meant to leave untouched.
/// </summary>
public sealed class PublicLogDefaultTests : IAsyncLifetime
{
    private sealed class NoPublicLogConfigured : RideLogApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["PublicLog:RiderId"] = null }));
        }
    }

    private readonly NoPublicLogConfigured _factory = new();

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = await users.FindByEmailAsync(RideLogApiFactory.AdminEmail);

        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.RemoveRange(context.Rides);
        context.Rides.Add(new Ride
        {
            Id = Guid.NewGuid(),
            UserId = admin!.Id,
            StartTime = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromMinutes(118),
            DistanceMeters = 61_500,
            Sport = "ROAD_BIKING",
            Source = RideSource.Polar,
        });
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    private sealed record ItemDto(Guid Id, double DistanceKm);

    private sealed record PagedDto(IReadOnlyList<ItemDto> Items, int Total);

    [Fact]
    public async Task Falls_back_to_the_seeded_admin_when_no_public_log_is_configured()
    {
        var page = await _factory.CreateClient().GetFromJsonAsync<PagedDto>("/rides");

        Assert.Equal(1, page!.Total);
        Assert.Equal(61.5, page.Items[0].DistanceKm, 0.1);
    }
}
