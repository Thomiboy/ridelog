namespace RideLog.Application.Auth;

/// <summary>
/// The sign-in providers this app will hand a rider to, and what comes back when one redirects them
/// home. Only the network half lives here — who the identity turns out to be is
/// <see cref="IExternalSignIn"/>'s decision, and it is the same decision whoever answered.
/// </summary>
public interface IExternalProviders
{
    /// <summary>Whether this app is configured to sign riders in with the named provider.</summary>
    bool Knows(string provider);

    string BuildAuthorizeUrl(string provider, string state);

    /// <summary>
    /// Trades the provider's authorization code for the identity behind it, or null when the
    /// provider will not say who it is.
    /// </summary>
    Task<ExternalIdentity?> IdentityForAsync(string provider, string code, CancellationToken cancellationToken = default);
}
