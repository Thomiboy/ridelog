namespace RideLog.Application.Polar;

/// <summary>
/// Talks to the Polar AccessLink API: the transaction-based exercise pull and OAuth token exchange.
/// Every call names the link it pulls with. The client used to fetch "the" stored link itself, which
/// with two riders meant pulling whoever happened to be first in the table.
/// </summary>
public interface IPolarClient
{
    /// <summary>Opens a transaction snapshotting new exercises, or null when there is nothing new.</summary>
    Task<PolarTransaction?> StartTransactionAsync(PolarToken link, CancellationToken cancellationToken = default);

    Task<PolarExercise> GetExerciseAsync(PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default);

    /// <summary>Downloads the exercise route as GPX, or null when the exercise has no GPS track.</summary>
    Task<byte[]?> DownloadGpxAsync(PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default);

    /// <summary>Downloads the exercise as TCX (HR/cadence), or null when unavailable.</summary>
    Task<byte[]?> DownloadTcxAsync(PolarToken link, string exerciseUrl, CancellationToken cancellationToken = default);

    /// <summary>Commits (acknowledges) the transaction so its exercises are not served again.</summary>
    Task CommitTransactionAsync(PolarToken link, PolarTransaction transaction, CancellationToken cancellationToken = default);
}
