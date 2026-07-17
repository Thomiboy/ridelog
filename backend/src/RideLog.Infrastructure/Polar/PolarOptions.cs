namespace RideLog.Infrastructure.Polar;

/// <summary>Polar AccessLink settings, bound from the "Polar" configuration section.</summary>
public sealed class PolarOptions
{
    public const string SectionName = "Polar";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public string AuthorizeUrl { get; set; } = "https://flow.polar.com/oauth2/authorization";
    public string TokenUrl { get; set; } = "https://polarremote.com/v2/oauth2/token";
    public string ApiBaseUrl { get; set; } = "https://www.polaraccesslink.com";

    /// <summary>OAuth2 redirect URI registered with the Polar client.</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Shared secret the sync cron sends instead of an admin JWT.</summary>
    public string SyncSharedSecret { get; set; } = string.Empty;
}
