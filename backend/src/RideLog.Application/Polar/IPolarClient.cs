namespace RideLog.Application.Polar;

/// <summary>Talks to the Polar AccessLink API: the transaction-based exercise pull and OAuth token exchange.</summary>
public interface IPolarClient
{
    /// <summary>Opens a transaction snapshotting new exercises, or null when there is nothing new.</summary>
    Task<PolarTransaction?> StartTransactionAsync(CancellationToken cancellationToken = default);

    Task<PolarExercise> GetExerciseAsync(string exerciseUrl, CancellationToken cancellationToken = default);

    /// <summary>Downloads the exercise route as GPX, or null when the exercise has no GPS track.</summary>
    Task<byte[]?> DownloadGpxAsync(string exerciseUrl, CancellationToken cancellationToken = default);

    /// <summary>Downloads the exercise as TCX (HR/cadence), or null when unavailable.</summary>
    Task<byte[]?> DownloadTcxAsync(string exerciseUrl, CancellationToken cancellationToken = default);

    /// <summary>Commits (acknowledges) the transaction so its exercises are not served again.</summary>
    Task CommitTransactionAsync(PolarTransaction transaction, CancellationToken cancellationToken = default);
}
