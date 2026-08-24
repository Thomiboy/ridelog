using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RideLog.Application.Auth;
using RideLog.Application.Rides;
using RideLog.Application.Settings;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.UnitTests.Auth;

/// <summary>
/// The owner's side of the door. Approving is the only thing that turns somebody who knocked into
/// somebody who is in, and rejecting an approved rider is what banning is — one switch, not two.
/// </summary>
public class RiderDirectoryTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);
    private sealed record RiderDto(
        string Id, string Email, string Approval, int RideCount, long StorageBytes, bool PolarLinked,
        bool IsPublicLog);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, RideLogApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<string> GivenRiderAsync(string email, Approval approval = Approval.Pending)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Rider>>();
        var rider = await users.FindByEmailAsync(email);
        if (rider is null)
        {
            rider = new Rider { UserName = email, Email = email, EmailConfirmed = true, Approval = approval };
            await users.CreateAsync(rider);
        }
        else
        {
            rider.Approval = approval;
            await users.UpdateAsync(rider);
        }

        return rider.Id;
    }

    private async Task<Approval> ApprovalOfAsync(string riderId)
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Rider>>();
        return (await users.FindByIdAsync(riderId))!.Approval;
    }

    [Fact]
    public async Task The_owner_lets_a_rider_in()
    {
        var riderId = await GivenRiderAsync("knocking@example.test");
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/riders/{riderId}/approval", new { approval = "Approved" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Approval.Approved, await ApprovalOfAsync(riderId));
    }

    /// <summary>
    /// Banning is not a separate capability: it is this switch, thrown the other way on somebody who
    /// was already in.
    /// </summary>
    [Fact]
    public async Task Rejecting_an_approved_rider_is_how_a_ban_is_spelled()
    {
        var riderId = await GivenRiderAsync("was-welcome@example.test", Approval.Approved);
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/riders/{riderId}/approval", new { approval = "Rejected" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Approval.Rejected, await ApprovalOfAsync(riderId));
    }

    [Fact]
    public async Task An_ordinary_rider_cannot_reach_the_directory()
    {
        var riderId = await GivenRiderAsync("nosy@example.test", Approval.Approved);
        using var scope = factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>()
            .CreateToken(riderId, "nosy@example.test", []);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var listed = await client.GetAsync("/riders");
        var changed = await client.PutAsJsonAsync($"/riders/{riderId}/approval", new { approval = "Approved" });

        Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, changed.StatusCode);
    }

    /// <summary>
    /// There is one admin. Shutting yourself out leaves nobody who can let you back in, and the fix
    /// is SQL against production — so the app refuses, and says why.
    /// </summary>
    [Fact]
    public async Task The_owner_cannot_shut_themselves_out()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Rider>>();
        var adminId = (await users.FindByEmailAsync(RideLogApiFactory.AdminEmail))!.Id;
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync($"/riders/{adminId}/approval", new { approval = "Rejected" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(Approval.Approved, await ApprovalOfAsync(adminId));
    }

    /// <summary>
    /// Rejecting the public-log rider would leave the public site showing a log nobody can tend —
    /// their sync stops, and they cannot sign in to do anything about it. The setting moves first.
    /// </summary>
    [Fact]
    public async Task The_public_log_rider_cannot_be_shut_out()
    {
        var riderId = await GivenRiderAsync("is-the-public-log@example.test", Approval.Approved);
        var publicLog = factory.Services.GetRequiredService<IOptions<PublicLogOptions>>().Value;
        var wasPublic = publicLog.RiderId;
        publicLog.RiderId = riderId;

        try
        {
            var admin = await AdminClientAsync();

            var response = await admin.PutAsJsonAsync($"/riders/{riderId}/approval", new { approval = "Rejected" });

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Equal(Approval.Approved, await ApprovalOfAsync(riderId));
        }
        finally
        {
            publicLog.RiderId = wasPublic;
        }
    }

    /// <summary>
    /// Only shutting someone out can strand the owner. Letting the public-log rider *in* is always
    /// safe, so the refusal must not fire on approval — it would block undoing a mistake.
    /// </summary>
    [Fact]
    public async Task Letting_the_public_log_rider_back_in_is_allowed()
    {
        var riderId = await GivenRiderAsync("public-log-returning@example.test", Approval.Rejected);
        var publicLog = factory.Services.GetRequiredService<IOptions<PublicLogOptions>>().Value;
        var wasPublic = publicLog.RiderId;
        publicLog.RiderId = riderId;

        try
        {
            var admin = await AdminClientAsync();

            var response = await admin.PutAsJsonAsync($"/riders/{riderId}/approval", new { approval = "Approved" });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(Approval.Approved, await ApprovalOfAsync(riderId));
        }
        finally
        {
            publicLog.RiderId = wasPublic;
        }
    }

    private int _rideNumber;

    private async Task GivenRideWithFileAsync(string riderId, int fileBytes)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        // One ride per rider per start time — the schema says so, and two rides for one rider is
        // exactly what this test needs.
        var start = new DateTimeOffset(2026, 6, 1, 8, 0, 0, TimeSpan.Zero).AddDays(_rideNumber++);
        var ride = new Ride
        {
            Id = Guid.NewGuid(),
            UserId = riderId,
            StartTime = start,
            EndTime = start.AddHours(1),
            Duration = TimeSpan.FromHours(1),
            DistanceMeters = 25_000,
            Sport = "ROAD_CYCLING",
            Source = RideSource.Polar,
        };
        ride.RawFiles.Add(new RawFile
        {
            Id = Guid.NewGuid(),
            UserId = riderId,
            Format = RawFileFormat.Tcx,
            FileName = "ride.tcx",
            Content = new byte[fileBytes],
            UploadedAt = DateTimeOffset.UtcNow,
        });
        context.Rides.Add(ride);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// The column the page exists for. Raw files share one 32 GB database, and without a per-rider
    /// figure the list is a list of names that cannot answer where the space went.
    /// </summary>
    [Fact]
    public async Task The_list_says_what_each_rider_is_using_and_not_what_everybody_is()
    {
        var heavy = await GivenRiderAsync("heavy@example.test", Approval.Approved);
        var light = await GivenRiderAsync("light@example.test", Approval.Approved);
        await GivenRideWithFileAsync(heavy, 5_000);
        await GivenRideWithFileAsync(heavy, 3_000);
        await GivenRideWithFileAsync(light, 1_000);
        var admin = await AdminClientAsync();

        var riders = await admin.GetFromJsonAsync<IReadOnlyList<RiderDto>>("/riders");

        var heavyRow = riders!.Single(rider => rider.Id == heavy);
        var lightRow = riders!.Single(rider => rider.Id == light);
        Assert.Equal(2, heavyRow.RideCount);
        Assert.Equal(8_000, heavyRow.StorageBytes);
        // The sharp half: a query that forgot to group would report 9,000 to everybody.
        Assert.Equal(1, lightRow.RideCount);
        Assert.Equal(1_000, lightRow.StorageBytes);
    }

    /// <summary>A rider with no rides reports nothing, rather than being missing from the list.</summary>
    [Fact]
    public async Task A_rider_who_has_never_ridden_still_appears_with_nothing()
    {
        var riderId = await GivenRiderAsync("no-rides@example.test", Approval.Approved);
        var admin = await AdminClientAsync();

        var riders = await admin.GetFromJsonAsync<IReadOnlyList<RiderDto>>("/riders");

        var row = riders!.Single(rider => rider.Id == riderId);
        Assert.Equal(0, row.RideCount);
        Assert.Equal(0, row.StorageBytes);
        Assert.False(row.PolarLinked);
    }

    /// <summary>
    /// #159 refuses to close the public-log rider's account with "point that setting at another
    /// rider first" — and until now there was no endpoint that wrote it, so the message prescribed
    /// an App Service edit and a restart. Moving it has to change what a visitor is actually served.
    /// </summary>
    [Fact]
    public async Task Moving_the_public_log_changes_whose_rides_a_visitor_sees()
    {
        var newcomer = await GivenRiderAsync("becomes-public@example.test", Approval.Approved);
        await GivenRideWithFileAsync(newcomer, 500);
        var publicLog = factory.Services.GetRequiredService<IOptions<PublicLogOptions>>().Value;
        var wasPublic = publicLog.RiderId;
        var admin = await AdminClientAsync();

        try
        {
            var moved = await admin.PutAsJsonAsync("/riders/public-log", new { riderId = newcomer });

            Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
            // Read as a visitor: no token at all, so the answer comes from the setting alone.
            var seen = await factory.CreateClient().GetFromJsonAsync<PagedDto>("/rides");
            Assert.Equal(1, seen!.Total);
        }
        finally
        {
            publicLog.RiderId = wasPublic;
        }
    }

    /// <summary>
    /// The defect in #172: moving the public log only mutated the in-memory options singleton, so
    /// the next process — which reads configuration, not memory — silently returned the log to the
    /// admin. Moving it has to write the store, where a separate reader (the next boot) finds it.
    /// </summary>
    [Fact]
    public async Task Moving_the_public_log_is_written_to_the_store()
    {
        var newcomer = await GivenRiderAsync("stored-public@example.test", Approval.Approved);
        var publicLog = factory.Services.GetRequiredService<IOptions<PublicLogOptions>>().Value;
        var wasPublic = publicLog.RiderId;
        var admin = await AdminClientAsync();

        try
        {
            var moved = await admin.PutAsJsonAsync("/riders/public-log", new { riderId = newcomer });
            Assert.Equal(HttpStatusCode.OK, moved.StatusCode);

            // Read the store in a fresh scope, not the singleton the endpoint also updated: this is
            // what the next boot does, and the value has to be there rather than only in memory.
            using var scope = factory.Services.CreateScope();
            var stored = await scope.ServiceProvider.GetRequiredService<ISettingsStore>()
                .GetAsync(SettingsKeys.PublicLogRiderId);
            Assert.Equal(newcomer, stored);
        }
        finally
        {
            publicLog.RiderId = wasPublic;
        }
    }

    /// <summary>A rider who is not in cannot be the face of the site.</summary>
    [Fact]
    public async Task The_public_log_cannot_be_pointed_at_a_rider_who_is_not_approved()
    {
        var waiting = await GivenRiderAsync("still-waiting@example.test");
        var admin = await AdminClientAsync();

        var response = await admin.PutAsJsonAsync("/riders/public-log", new { riderId = waiting });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private sealed record PagedDto(int Total);

    /// <summary>
    /// The list has to say which rider is the public one, or the two refusals that protect them
    /// arrive as a surprise — and moving the setting means picking blindly.
    /// </summary>
    [Fact]
    public async Task The_list_marks_which_rider_is_the_public_log()
    {
        var riderId = await GivenRiderAsync("marked-public@example.test", Approval.Approved);
        var publicLog = factory.Services.GetRequiredService<IOptions<PublicLogOptions>>().Value;
        var wasPublic = publicLog.RiderId;
        publicLog.RiderId = riderId;

        try
        {
            var riders = await (await AdminClientAsync()).GetFromJsonAsync<IReadOnlyList<RiderDto>>("/riders");

            Assert.True(riders!.Single(rider => rider.Id == riderId).IsPublicLog);
            Assert.All(riders.Where(rider => rider.Id != riderId), rider => Assert.False(rider.IsPublicLog));
        }
        finally
        {
            publicLog.RiderId = wasPublic;
        }
    }

    [Fact]
    public async Task The_list_names_every_rider_and_where_they_stand()
    {
        await GivenRiderAsync("listed-pending@example.test");
        await GivenRiderAsync("listed-approved@example.test", Approval.Approved);
        var admin = await AdminClientAsync();

        var riders = await admin.GetFromJsonAsync<IReadOnlyList<RiderDto>>("/riders");

        Assert.Contains(riders!, rider => rider.Email == "listed-pending@example.test" && rider.Approval == "Pending");
        Assert.Contains(riders!, rider => rider.Email == "listed-approved@example.test" && rider.Approval == "Approved");
    }
}
