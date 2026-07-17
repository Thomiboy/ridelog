namespace RideLog.Application.Polar;

/// <summary>The Polar AccessLink OAuth2 authorization-code flow.</summary>
public interface IPolarOAuth
{
    /// <summary>The Polar authorization URL to redirect the admin to.</summary>
    string BuildAuthorizeUrl(string state);

    /// <summary>Exchanges an authorization code for a (non-expiring) access token and links the AccessLink user.</summary>
    Task<PolarToken> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
}
