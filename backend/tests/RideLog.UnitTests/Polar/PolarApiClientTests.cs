using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RideLog.Application.Polar;
using RideLog.Infrastructure.Polar;

namespace RideLog.UnitTests.Polar;

public class PolarApiClientTests
{
    private const string BaseUrl = "https://api.polar.test";

    private sealed class StubTokenStore : IPolarTokenStore
    {
        public Task SaveAsync(string appUserId, PolarToken token, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<PolarConnectionInfo?> GetConnectionAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<PolarConnectionInfo?>(new PolarConnectionInfo("admin-1", new PolarToken("access-tok", "pu-1")));

        public Task<PolarStatus> GetStatusAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new PolarStatus(true, DateTimeOffset.UtcNow, null, null));
    }

    private static PolarApiClient NewClient(MockHttpMessageHandler handler)
    {
        var http = new HttpClient(handler);
        var options = Options.Create(new PolarOptions { ApiBaseUrl = BaseUrl });
        return new PolarApiClient(http, new StubTokenStore(), options);
    }

    [Fact]
    public async Task Start_transaction_creates_then_lists_exercises_with_a_bearer_token()
    {
        var handler = new MockHttpMessageHandler(request =>
            request.Method == HttpMethod.Post
                ? MockHttpMessageHandler.Json("""{ "transaction-id": 4242 }""")
                : MockHttpMessageHandler.Json("""{ "exercises": ["https://api.polar.test/ex/1", "https://api.polar.test/ex/2"] }"""));

        var transaction = await NewClient(handler).StartTransactionAsync();

        Assert.NotNull(transaction);
        Assert.Equal("4242", transaction.Id);
        Assert.Equal(2, transaction.ExerciseUrls.Count);

        var createRequest = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, createRequest.Method);
        Assert.Equal($"{BaseUrl}/v3/users/pu-1/exercise-transactions", createRequest.RequestUri!.ToString());
        Assert.Equal("access-tok", createRequest.Headers.Authorization!.Parameter);
        Assert.Equal("Bearer", createRequest.Headers.Authorization.Scheme);
    }

    [Fact]
    public async Task Start_transaction_returns_null_when_nothing_is_new()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Status(HttpStatusCode.NoContent));

        Assert.Null(await NewClient(handler).StartTransactionAsync());
    }

    [Fact]
    public async Task Get_exercise_parses_start_time_and_sport()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json(
            """{ "start-time": "2026-06-10T06:00:00.000Z", "detailed-sport-info": "ROAD_BIKING" }"""));

        var exercise = await NewClient(handler).GetExerciseAsync($"{BaseUrl}/ex/1");

        Assert.Equal(new DateTimeOffset(2026, 6, 10, 6, 0, 0, TimeSpan.Zero), exercise.StartTime);
        Assert.Equal("ROAD_BIKING", exercise.Sport);
    }

    [Fact]
    public async Task Download_gpx_returns_the_bytes_and_requests_the_gpx_sub_resource()
    {
        var payload = Encoding.UTF8.GetBytes("<gpx/>");
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Bytes(payload));

        var bytes = await NewClient(handler).DownloadGpxAsync($"{BaseUrl}/ex/1");

        Assert.Equal(payload, bytes);
        Assert.Equal($"{BaseUrl}/ex/1/gpx", handler.Requests[0].RequestUri!.ToString());
    }

    [Fact]
    public async Task Download_tcx_returns_null_when_absent()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Status(HttpStatusCode.NotFound));

        Assert.Null(await NewClient(handler).DownloadTcxAsync($"{BaseUrl}/ex/1"));
    }

    [Fact]
    public async Task Commit_transaction_puts_to_the_transaction_url()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Status(HttpStatusCode.OK));

        await NewClient(handler).CommitTransactionAsync(new PolarTransaction("4242", []));

        Assert.Equal(HttpMethod.Put, handler.Requests[0].Method);
        Assert.Equal($"{BaseUrl}/v3/users/pu-1/exercise-transactions/4242", handler.Requests[0].RequestUri!.ToString());
    }
}
