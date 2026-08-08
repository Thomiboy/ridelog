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

/// <summary>
/// Leaving. Distinct from emptying a log: deleting rides is maintenance and the Polar link keeps
/// delivering afterwards, whereas closing an account takes the rides, the link and the login
/// together so there is nothing left to sign back into.
/// </summary>
public interface IRiderAccounts
{
    Task<AccountClosure> CloseAsync(string riderId, CancellationToken cancellationToken = default);
}
