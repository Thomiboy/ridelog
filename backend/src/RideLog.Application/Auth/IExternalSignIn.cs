namespace RideLog.Application.Auth;

/// <summary>
/// What a provider hands over about whoever just signed in. <paramref name="Subject"/> is the
/// provider's own id for them — stable across email changes, which the address is not.
/// </summary>
public sealed record ExternalIdentity(string Provider, string Subject, string Email, bool EmailVerified);

/// <summary>
/// The rider a provider's identity resolved to, and where they stand. Arriving is not the same as
/// being let in: registration is open, so the caller has to know which happened.
/// </summary>
public sealed record ExternalSignInResult(string RiderId, Approval Approval);

/// <summary>
/// Turns an identity a provider vouched for into a rider. New riders arrive this way and no other:
/// nothing in this codebase sends email, so a local password would have no verification and no
/// reset (docs/adr/0007).
/// </summary>
public interface IExternalSignIn
{
    /// <summary>Returns the rider, or null when the identity is refused.</summary>
    Task<ExternalSignInResult?> SignInAsync(ExternalIdentity identity, CancellationToken cancellationToken = default);
}
