using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RideLog.Application.Polar;

namespace RideLog.Infrastructure.Polar;

/// <summary>
/// Talks to the Polar AccessLink REST API using the transaction model: create a transaction,
/// list its exercises, download GPX/TCX, then commit to acknowledge. The stored access token is
/// attached as a bearer on every call.
/// </summary>
internal sealed class PolarApiClient(
    HttpClient http,
    IPolarTokenStore tokenStore,
    IOptions<PolarOptions> options) : IPolarClient
{
    private readonly string _baseUrl = options.Value.ApiBaseUrl.TrimEnd('/');

    public async Task<PolarTransaction?> StartTransactionAsync(CancellationToken cancellationToken = default)
    {
        var (userId, _) = await AuthContextAsync(cancellationToken);
        var transactionsUrl = $"{_baseUrl}/v3/users/{userId}/exercise-transactions";

        using var createRequest = await AuthorizedAsync(HttpMethod.Post, transactionsUrl, cancellationToken);
        using var createResponse = await http.SendAsync(createRequest, cancellationToken);
        if (createResponse.StatusCode == HttpStatusCode.NoContent)
        {
            return null; // nothing new to pull
        }
        createResponse.EnsureSuccessStatusCode();

        using var created = await ReadJsonAsync(createResponse, cancellationToken);
        var transactionId = created.RootElement.GetProperty("transaction-id").ToString();

        using var listRequest = await AuthorizedAsync(HttpMethod.Get, $"{transactionsUrl}/{transactionId}", cancellationToken);
        using var listResponse = await http.SendAsync(listRequest, cancellationToken);
        listResponse.EnsureSuccessStatusCode();

        using var listed = await ReadJsonAsync(listResponse, cancellationToken);
        var exercises = listed.RootElement.TryGetProperty("exercises", out var array)
            ? array.EnumerateArray().Select(e => e.GetString()!).ToList()
            : [];

        return new PolarTransaction(transactionId, exercises);
    }

    public async Task<PolarExercise> GetExerciseAsync(string exerciseUrl, CancellationToken cancellationToken = default)
    {
        using var request = await AuthorizedAsync(HttpMethod.Get, exerciseUrl, cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = await ReadJsonAsync(response, cancellationToken);
        var root = json.RootElement;

        var startTime = ParseStartTime(root);
        var sport = root.TryGetProperty("detailed-sport-info", out var detailed) && detailed.GetString() is { Length: > 0 } d
            ? d
            : root.TryGetProperty("sport", out var s) ? s.GetString() ?? "Unknown" : "Unknown";

        return new PolarExercise(exerciseUrl, startTime, sport);
    }

    public Task<byte[]?> DownloadGpxAsync(string exerciseUrl, CancellationToken cancellationToken = default) =>
        DownloadAsync($"{exerciseUrl}/gpx", cancellationToken);

    public Task<byte[]?> DownloadTcxAsync(string exerciseUrl, CancellationToken cancellationToken = default) =>
        DownloadAsync($"{exerciseUrl}/tcx", cancellationToken);

    public async Task CommitTransactionAsync(PolarTransaction transaction, CancellationToken cancellationToken = default)
    {
        var (userId, _) = await AuthContextAsync(cancellationToken);
        var url = $"{_baseUrl}/v3/users/{userId}/exercise-transactions/{transaction.Id}";

        using var request = await AuthorizedAsync(HttpMethod.Put, url, cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<byte[]?> DownloadAsync(string url, CancellationToken cancellationToken)
    {
        using var request = await AuthorizedAsync(HttpMethod.Get, url, cancellationToken);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private async Task<(string PolarUserId, string AccessToken)> AuthContextAsync(CancellationToken cancellationToken)
    {
        var connection = await tokenStore.GetConnectionAsync(cancellationToken)
            ?? throw new InvalidOperationException("No Polar account is linked.");
        return (connection.Token.PolarUserId, connection.Token.AccessToken);
    }

    private async Task<HttpRequestMessage> AuthorizedAsync(HttpMethod method, string url, CancellationToken cancellationToken)
    {
        var (_, accessToken) = await AuthContextAsync(cancellationToken);
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static DateTimeOffset ParseStartTime(JsonElement root)
    {
        var raw = root.GetProperty("start-time").GetString()!;
        var parsed = DateTimeOffset.Parse(raw, CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind | DateTimeStyles.AssumeUniversal);

        // Polar may send local time plus a separate offset in minutes.
        if (root.TryGetProperty("start-time-zone-offset", out var offset) && offset.TryGetInt32(out var minutes))
        {
            parsed = new DateTimeOffset(parsed.DateTime, TimeSpan.FromMinutes(minutes));
        }

        return parsed;
    }
}
