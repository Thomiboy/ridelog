using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RideLog.Application.Auth;

namespace RideLog.Infrastructure.Auth;

/// <summary>
/// The OpenID Connect authorization-code round trip, written the way <c>PolarOAuthClient</c> writes
/// it rather than through Identity's external-login plumbing: that plumbing keeps its correlation in
/// a cookie, and this app is deliberately cookie-free across two origins (docs/adr/0007).
/// </summary>
internal sealed class ExternalProviders(HttpClient http, IOptions<ExternalSignInOptions> options) : IExternalProviders
{
    private readonly ExternalSignInOptions _options = options.Value;

    public bool Knows(string provider) =>
        Settings(provider) is { ClientId.Length: > 0 };

    public string BuildAuthorizeUrl(string provider, string state)
    {
        var settings = Settings(provider) ?? throw new InvalidOperationException($"Unknown provider '{provider}'.");
        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["client_id"] = settings.ClientId,
            ["redirect_uri"] = RedirectUri(provider),
            ["scope"] = settings.Scope,
            ["state"] = state,
        };

        return $"{settings.AuthorizeUrl}?{string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"))}";
    }

    public async Task<ExternalIdentity?> IdentityForAsync(
        string provider, string code, CancellationToken cancellationToken = default)
    {
        var settings = Settings(provider);
        if (settings is null)
        {
            return null;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, settings.TokenUrl)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = RedirectUri(provider),
                ["client_id"] = settings.ClientId,
                ["client_secret"] = settings.ClientSecret,
            }),
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!json.RootElement.TryGetProperty("id_token", out var idToken) || idToken.GetString() is not { } jwt)
        {
            return null;
        }

        return Read(provider, jwt, settings);
    }

    /// <summary>
    /// Read, not validated. The token came back over TLS from the provider's own token endpoint in
    /// answer to a code this app just sent, which is the one case OpenID Connect says the signature
    /// need not be checked (core §3.1.3.7). Nothing else here accepts an id_token.
    /// </summary>
    private static ExternalIdentity? Read(string provider, string jwt, ExternalProviderSettings settings)
    {
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(jwt).Claims.ToLookup(claim => claim.Type);

        var subject = claims["sub"].FirstOrDefault()?.Value;
        var email = claims["email"].FirstOrDefault()?.Value;
        if (subject is null || string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var stated = claims["email_verified"].FirstOrDefault()?.Value;
        var verified = stated is null
            ? settings.EmailVerifiedWhenUnstated
            : bool.TryParse(stated, out var said) && said;

        return new ExternalIdentity(provider, subject, email, verified);
    }

    private ExternalProviderSettings? Settings(string provider) =>
        _options.Providers.TryGetValue(provider, out var settings) ? settings : null;

    private string RedirectUri(string provider) =>
        _options.RedirectUriTemplate.Replace("{provider}", provider, StringComparison.OrdinalIgnoreCase);
}
