namespace RideLog.Application.Polar;

/// <summary>Persists the Polar access token, encrypted at rest. Single connection in the MVP; schema is multi-user-ready.</summary>
public interface IPolarTokenStore
{
    Task SaveAsync(string appUserId, PolarToken token, CancellationToken cancellationToken = default);

    /// <summary>The current Polar connection, or null when no account is linked yet.</summary>
    Task<PolarConnectionInfo?> GetConnectionAsync(CancellationToken cancellationToken = default);
}
