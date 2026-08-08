namespace RideLog.Application.Rides;

/// <summary>
/// Which rider's log a visitor sees without signing in. A setting rather than a role: today the same
/// person holds both, but one is what a rider may do and the other is whose rides the world can see,
/// and deriving the second from the first would make the showcase follow a permission flag.
///
/// It lives in Application rather than at the edge because it is not only an endpoint's concern:
/// whether a rider may leave depends on it (docs/adr/0006).
/// </summary>
public sealed class PublicLogOptions
{
    public const string SectionName = "PublicLog";

    /// <summary>The rider whose log is public; empty means nothing is.</summary>
    public string RiderId { get; set; } = string.Empty;
}
