using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RideLog.Application.Auth;
using RideLog.Application.Polar;
using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Rides;

/// <summary>
/// Opening the app to strangers without a way out is not something to ship on purpose. Closing an
/// account is the way out, and it is a different act from emptying the log: it takes the rider's
/// rides, their Polar link and their login together, so nothing is left to sign back into.
/// </summary>
public class AccountClosureTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private async Task<(HttpClient Client, string RiderId)> RiderClientAsync(string email)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var rider = await users.FindByEmailAsync(email);
        if (rider is null)
        {
            rider = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
            await users.CreateAsync(rider);
        }

        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateToken(rider.Id, email, []);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        return (client, rider.Id);
    }

    private async Task GivenARideAndALinkAsync(string riderId)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.Rides.Add(new Ride
        {
            Id = Guid.NewGuid(),
            UserId = riderId,
            StartTime = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromHours(1),
            DistanceMeters = 25_000,
            Sport = "ROAD_CYCLING",
            Source = RideSource.Polar,
        });
        await context.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IPolarTokenStore>()
            .SaveAsync(riderId, new PolarToken("tok", $"pu-{riderId}"));
    }

    [Fact]
    public async Task Closing_an_account_takes_the_rides_the_link_and_the_login_together()
    {
        var (client, riderId) = await RiderClientAsync("leaves@example.test");
        await GivenARideAndALinkAsync(riderId);

        var response = await client.DeleteAsync("/account");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        Assert.Empty(context.Rides.Where(ride => ride.UserId == riderId));
        Assert.Empty(context.PolarConnections.Where(link => link.UserId == riderId));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        Assert.Null(await users.FindByIdAsync(riderId));
    }

    /// <summary>
    /// The public log is a setting naming one rider, so closing that rider's account would blank
    /// the site one click, with nothing on screen to say why. The setting has to move first.
    /// </summary>
    [Fact]
    public async Task Closing_the_account_that_is_the_public_log_is_refused()
    {
        var (client, riderId) = await RiderClientAsync("is-the-public-log@example.test");
        var publicLog = factory.Services.GetRequiredService<IOptions<PublicLogOptions>>().Value;
        var wasPublic = publicLog.RiderId;
        publicLog.RiderId = riderId;

        try
        {
            var response = await client.DeleteAsync("/account");

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.NotNull(await users.FindByIdAsync(riderId));
        }
        finally
        {
            publicLog.RiderId = wasPublic;
        }
    }
}
