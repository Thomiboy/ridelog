namespace RideLog.Application.Auth;

/// <summary>Authenticates credentials and issues access tokens, keeping the identity provider out of the API layer.</summary>
public interface IAuthService
{
    /// <summary>Returns an access token for valid credentials, or null when authentication fails.</summary>
    Task<AccessToken?> LoginAsync(string email, string password, CancellationToken cancellationToken = default);
}
