namespace RideLog.Application.Polar;

/// <summary>
/// Persists each rider's Polar access token, encrypted at rest. Every read names its rider: the
/// lookups used to return the first row in the table, which with two riders is somebody else's.
/// </summary>
public interface IPolarTokenStore
{
    Task SaveAsync(string appUserId, PolarToken token, CancellationToken cancellationToken = default);

    /// <summary>A rider's own Polar link, or null when that rider has not linked one.</summary>
    Task<PolarConnectionInfo?> GetConnectionAsync(string riderId, CancellationToken cancellationToken = default);

    /// <summary>Every rider who has linked Polar — who the daily run is for.</summary>
    Task<IReadOnlyList<string>> GetLinkedRidersAsync(CancellationToken cancellationToken = default);

    /// <summary>Link and last-sync state, for the rider whose card is being shown.</summary>
    Task<PolarStatus> GetStatusAsync(string riderId, CancellationToken cancellationToken = default);
}
