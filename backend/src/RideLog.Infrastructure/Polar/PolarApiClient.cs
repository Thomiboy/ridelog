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
    IOptions<PolarOptions> options) : IPolarClient
{
    private readonly string _baseUrl = options.Value.ApiBaseUrl.TrimEnd('/');

    public async Task<PolarTransaction?> StartTransactionAsync(PolarToken link, CancellationToken cancellationToken = default)
    {
        var transactionsUrl = $"{_baseUrl}/v3/users/{link.PolarUserId}/exercise-transactions";

        using var createRequest = Authorized(link, HttpMethod.Post, transactionsUrl);
        using var createResponse = await http.SendAsync(createRequest, cancellationToken);
        if (createResponse.StatusCode == HttpStatusCode.NoContent)
        {
            return null; // nothing new to pull
        }
        createResponse.EnsureSuccessStatusCode();

        using var created = await ReadJsonAsync(createResponse, cancellationToken);
        var transactionId = created.RootElement.GetProperty("transaction-id").ToString();

        using var listRequest = Authorized(link, HttpMethod.Get, $"{transactionsUrl}/{transactionId}");
        using var listResponse = await http.SendAsync(listRequest, cancellationToken);
        listResponse.EnsureSuccessStatusCode();

        using var listed = await ReadJsonAsync(listResponse, cancellationToken);
        var exercises = listed.RootElement.TryGetProperty("exercises", out var array)
            ? array.EnumerateArray().Select(e => e.GetString()!).ToList()
            : [];

        return new PolarTransaction(transactionId, exercises);
    }

    public async Task<PolarExercise> GetExerciseAsync(
        PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default)
    {
        using var request = Authorized(link, HttpMethod.Get, exerciseUrl);
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

    // The GPX/TCX sub-resources are XML files, not JSON — asking for application/json returns 406.
    public Task<byte[]?> DownloadGpxAsync(
        PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default) =>
        DownloadAsync(link, $"{exerciseUrl}/gpx", "application/gpx+xml", cancellationToken);

    public Task<byte[]?> DownloadTcxAsync(
        PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default) =>
        DownloadAsync(link, $"{exerciseUrl}/tcx", "application/vnd.garmin.tcx+xml", cancellationToken);

    public async Task CommitTransactionAsync(
        PolarToken link, PolarTransaction transaction, CancellationToken cancellationToken = default)
    {
        var url = $"{_baseUrl}/v3/users/{link.PolarUserId}/exercise-transactions/{transaction.Id}";

        using var request = Authorized(link, HttpMethod.Put, url);
        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task<byte[]?> DownloadAsync(
        PolarToken link, string url, string accept, CancellationToken cancellationToken)
    {
        using var request = Authorized(link, HttpMethod.Get, url, accept);
        using var response = await http.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }

    private static HttpRequestMessage Authorized(
        PolarToken link, HttpMethod method, string url, string accept = "application/json")
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", link.AccessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(accept));
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
