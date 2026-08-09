using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RideLog.Application.Auth;
using RideLog.Application.Rides;
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
    private sealed record RiderDto(string Id, string Email, string Approval);

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
