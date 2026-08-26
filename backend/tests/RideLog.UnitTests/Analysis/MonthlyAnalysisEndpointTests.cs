using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RideLog.Application.Analysis;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Analysis;

/// <summary>
/// The monthly analysis over HTTP (#187). The model is faked throughout — this is about what the
/// endpoint asks of it and what it refuses to ask at all, not about whether Anthropic answers.
/// </summary>
public class MonthlyAnalysisEndpointTests(AnalysisApiFactory factory) : IClassFixture<AnalysisApiFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);
    private sealed record WriteRequest(int Year, int Month, string Language);
    private sealed record AnalysisDto(
        Guid Id, int Year, int Month, string Language, string Text, string Model, int RideCount,
        DateTimeOffset WrittenAt);
    private sealed record RefusalDto(string Refusal);
    private sealed record StatisticsDto(bool AnalysisAvailable);

    private async Task<HttpClient> RiderClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, RideLogApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task ResetAsync(bool available = true)
    {
        factory.Analyst.Asked.Clear();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.MonthlyAnalyses.RemoveRange(context.MonthlyAnalyses);
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.Rides.Add(new Ride
        {
            Id = Guid.NewGuid(),
            UserId = "admin-1",
            StartTime = new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero),
            EndTime = new DateTimeOffset(2026, 6, 10, 10, 0, 0, TimeSpan.Zero),
            Duration = TimeSpan.FromHours(2),
            DistanceMeters = 40_000,
            Sport = "ROAD_BIKING",
            Source = RideSource.Polar,
        });
        await context.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IMonthlyAnalysisService>().SetAvailableAsync(available);
    }

    /// <summary>An analysis is about one rider's own log, so there is nothing here to read signed out.</summary>
    [Fact]
    public async Task A_visitor_who_is_not_signed_in_gets_nothing()
    {
        await ResetAsync();
        var client = factory.CreateClient();

        var written = await client.PostAsJsonAsync("/statistics/analysis", new WriteRequest(2026, 6, "Hungarian"));
        var read = await client.GetAsync("/statistics/analysis?year=2026&month=6&language=Hungarian");

        Assert.Equal(HttpStatusCode.Unauthorized, written.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, read.StatusCode);
        Assert.Empty(factory.Analyst.Asked);
    }

    [Fact]
    public async Task A_rider_writes_their_month_and_reads_it_back()
    {
        await ResetAsync();
        var client = await RiderClientAsync();

        var response = await client.PostAsJsonAsync("/statistics/analysis", new WriteRequest(2026, 6, "Hungarian"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var written = await response.Content.ReadFromJsonAsync<AnalysisDto>();
        Assert.Equal("Reading of 2026-6 in Hungarian.", written!.Text);

        // The language travels as its name, never as an ordinal (#187).
        Assert.Equal("Hungarian", written.Language);

        var read = await client.GetFromJsonAsync<AnalysisDto>("/statistics/analysis?year=2026&month=6&language=Hungarian");
        Assert.Equal(written.Id, read!.Id);
    }

    /// <summary>A month with no analysis is a miss, not an error — the section renders its button.</summary>
    [Fact]
    public async Task A_month_that_has_none_reads_as_not_found()
    {
        await ResetAsync();
        var client = await RiderClientAsync();

        var response = await client.GetAsync("/statistics/analysis?year=2026&month=5&language=Hungarian");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The refusal is named in the answer, because the page has different things to say about each:
    /// one asks the rider to delete first, the other tells them to go and ride.
    /// </summary>
    [Fact]
    public async Task Asking_twice_for_a_closed_month_is_a_named_refusal_and_asks_the_model_once()
    {
        await ResetAsync();
        var client = await RiderClientAsync();
        await client.PostAsJsonAsync("/statistics/analysis", new WriteRequest(2026, 6, "Hungarian"));

        var second = await client.PostAsJsonAsync("/statistics/analysis", new WriteRequest(2026, 6, "Hungarian"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("AlreadyWritten", (await second.Content.ReadFromJsonAsync<RefusalDto>())!.Refusal);
        Assert.Single(factory.Analyst.Asked);
    }

    /// <summary>
    /// The switch is enforced here, not by hiding the section — the same rule as the contact form's
    /// (#168): a request that skips the page still has to meet it.
    /// </summary>
    [Fact]
    public async Task With_the_switch_off_the_endpoint_refuses_even_though_the_page_would_hide_it()
    {
        await ResetAsync(available: false);
        var client = await RiderClientAsync();

        var response = await client.PostAsJsonAsync("/statistics/analysis", new WriteRequest(2026, 6, "Hungarian"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Empty(factory.Analyst.Asked);
    }

    [Fact]
    public async Task A_rider_deletes_their_own_analysis_and_can_write_it_again()
    {
        await ResetAsync();
        var client = await RiderClientAsync();
        var written = await (await client.PostAsJsonAsync("/statistics/analysis", new WriteRequest(2026, 6, "Hungarian")))
            .Content.ReadFromJsonAsync<AnalysisDto>();

        var deleted = await client.DeleteAsync($"/statistics/analysis/{written!.Id}");
        var again = await client.PostAsJsonAsync("/statistics/analysis", new WriteRequest(2026, 6, "Hungarian"));

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, factory.Analyst.Asked.Count);
    }

    /// <summary>
    /// Deleting something that is not yours is a miss rather than a refusal: a refusal would confirm
    /// it exists (docs/adr/0006).
    /// </summary>
    [Fact]
    public async Task Deleting_an_analysis_that_is_not_yours_is_a_miss()
    {
        await ResetAsync();
        var client = await RiderClientAsync();

        var response = await client.DeleteAsync($"/statistics/analysis/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// The page asks the API whether the section exists rather than guessing — the wart the login
    /// page still carries, where an unconfigured provider leaves a live-looking dead link (#186).
    /// </summary>
    [Fact]
    public async Task The_statistics_feed_says_whether_the_section_is_there()
    {
        await ResetAsync(available: false);
        var off = await factory.CreateClient().GetFromJsonAsync<StatisticsDto>("/statistics");
        Assert.False(off!.AnalysisAvailable);

        await ResetAsync(available: true);
        var on = await factory.CreateClient().GetFromJsonAsync<StatisticsDto>("/statistics");
        Assert.True(on!.AnalysisAvailable);
    }
}
