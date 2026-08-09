namespace RideLog.Application.Auth;

/// <summary>
/// Where a rider stands with the owner. Registration is open — anyone with a Google or Microsoft
/// account can arrive — so arriving and being let in are different things.
/// </summary>
public enum Approval
{
    /// <summary>Knocked, not yet decided about. Where every new rider starts.</summary>
    Pending,

    /// <summary>Let in. The only state that is issued a token.</summary>
    Approved,

    /// <summary>
    /// Turned away. Also what banning is: rejecting a rider who was approved. One state rather than
    /// two, because "I said no" is the same fact however late it is said — what it is *not* is
    /// <see cref="Pending"/>, which is why the two are told apart at all.
    /// </summary>
    Rejected,
}
