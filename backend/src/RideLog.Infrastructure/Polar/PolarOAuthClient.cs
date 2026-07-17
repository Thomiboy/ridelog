using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RideLog.Application.Polar;

namespace RideLog.Infrastructure.Polar;

/// <summary>
/// Implements the AccessLink OAuth2 authorization-code flow: build the authorize URL, exchange the
/// code for a non-expiring token, and register the member with AccessLink (idempotent).
/// </summary>
internal sealed class PolarOAuthClient(HttpClient http, IOptions<PolarOptions> options) : IPolarOAuth
{
    private readonly PolarOptions _options = options.Value;

    public string BuildAuthorizeUrl(string state)
    {
        var query = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _options.ClientId,
            ["redirect_uri"] = _options.RedirectUri,
            ["scope"] = "accesslink.read_all",
            ["state"] = state,
        };
        var encoded = string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value ?? string.Empty)}"));
        return $"{_options.AuthorizeUrl}?{encoded}";
    }

    public async Task<PolarToken> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _options.RedirectUri,
            }),
        };
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ClientId}:{_options.ClientSecret}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var accessToken = json.RootElement.GetProperty("access_token").GetString()!;
        var polarUserId = json.RootElement.GetProperty("x_user_id").ToString();

        await RegisterMemberAsync(accessToken, polarUserId, cancellationToken);

        return new PolarToken(accessToken, polarUserId);
    }

    private async Task RegisterMemberAsync(string accessToken, string polarUserId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.ApiBaseUrl.TrimEnd('/')}/v3/users")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { member_id = polarUserId }), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, cancellationToken);

        // 409 Conflict means the member is already registered — that is fine.
        if (response.StatusCode != HttpStatusCode.Conflict)
        {
            response.EnsureSuccessStatusCode();
        }
    }
}
