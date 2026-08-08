namespace RideLog.Infrastructure.Auth;

/// <summary>Sign-in provider settings, bound from the "ExternalSignIn" configuration section.</summary>
public sealed class ExternalSignInOptions
{
    public const string SectionName = "ExternalSignIn";

    /// <summary>
    /// Where the provider sends the browser back, with <c>{provider}</c> standing for the name. It
    /// has to match what is registered with the provider, so it is configured rather than guessed
    /// from the incoming request — behind App Service the request's own host is not always the one
    /// the browser used.
    /// </summary>
    public string RedirectUriTemplate { get; set; } = string.Empty;

    /// <summary>The provider names this app is willing to use, with the endpoints of each.</summary>
    public Dictionary<string, ExternalProviderSettings> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["google"] = new()
        {
            AuthorizeUrl = "https://accounts.google.com/o/oauth2/v2/auth",
            TokenUrl = "https://oauth2.googleapis.com/token",
        },
        ["microsoft"] = new()
        {
            AuthorizeUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/authorize",
            TokenUrl = "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            // Microsoft's id_token carries no email_verified claim at all, so absent cannot mean
            // "unverified" here without refusing every Microsoft rider. Google does send it.
            EmailVerifiedWhenUnstated = true,
        },
    };
}

public sealed class ExternalProviderSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string AuthorizeUrl { get; set; } = string.Empty;
    public string TokenUrl { get; set; } = string.Empty;
    public string Scope { get; set; } = "openid email";

    /// <summary>Whether an id_token without an <c>email_verified</c> claim counts as verified.</summary>
    public bool EmailVerifiedWhenUnstated { get; set; }
}
