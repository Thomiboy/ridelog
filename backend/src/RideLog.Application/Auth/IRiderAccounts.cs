namespace RideLog.Application.Auth;

/// <summary>What came of a rider asking to leave.</summary>
public enum AccountClosure
{
    /// <summary>The rider, their rides, their raw files and their Polar link are gone.</summary>
    Closed,

    /// <summary>
    /// Refused: this rider is the configured public log, and closing them would blank the public
    /// site. The setting has to name somebody else first.
    /// </summary>
    RefusedPublicLog,

    /// <summary>There is no such rider — already closed, or never existed.</summary>
    UnknownRider,
}

/// <summary>One rider as the owner sees them on the riders page.</summary>
public sealed record RiderSummary(string Id, string Email, Approval Approval);

/// <summary>Why a change of approval was refused, or that it went through.</summary>
public enum ApprovalChange
{
    Changed,

    /// <summary>
    /// Refused: nobody may shut themselves out. There is one admin, so locking yourself out leaves
    /// nobody who can let you back in.
    /// </summary>
    RefusedSelf,

    /// <summary>
    /// Refused: this rider is the configured public log, and shutting them out would leave the
    /// public site showing a log that can no longer be tended.
    /// </summary>
    RefusedPublicLog,

    UnknownRider,
}

/// <summary>
/// Leaving. Distinct from emptying a log: deleting rides is maintenance and the Polar link keeps
/// delivering afterwards, whereas closing an account takes the rides, the link and the login
/// together so there is nothing left to sign back into.
/// </summary>
public interface IRiderAccounts
{
    Task<AccountClosure> CloseAsync(string riderId, CancellationToken cancellationToken = default);

    /// <summary>Every rider and where they stand, for the owner's riders page.</summary>
    Task<IReadOnlyList<RiderSummary>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Moves a rider between pending, approved and rejected. <paramref name="actingRiderId"/> is
    /// who is doing it, because the one thing nobody may do is shut themselves out.
    /// </summary>
    Task<ApprovalChange> SetApprovalAsync(
        string actingRiderId, string riderId, Approval approval, CancellationToken cancellationToken = default);
}
