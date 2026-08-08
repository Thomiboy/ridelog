namespace RideLog.Application.Auth;

/// <summary>
/// The short-lived, single-use code a provider's callback hands the browser in place of a token. The
/// frontend exchanges it for the JWT, so the token never reaches a URL — where it would outlive the
/// sign-in in browser history (docs/adr/0007).
/// </summary>
public interface ISignInCodes
{
    string Issue(string riderId);

    /// <summary>Returns the rider the code stands for and spends it, or null if it was already spent or has expired.</summary>
    string? Redeem(string code);
}
