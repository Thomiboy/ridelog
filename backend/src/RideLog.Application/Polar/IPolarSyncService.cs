namespace RideLog.Application.Polar;

/// <summary>Pulls new Polar exercises and lands them as rides, idempotently.</summary>
public interface IPolarSyncService
{
    Task<SyncSummary> SyncAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// The daily run: every rider who has linked Polar, each pulled with their own link. One rider's
    /// failure is theirs — the run carries on and reports it against them.
    /// </summary>
    Task<IReadOnlyList<RiderSyncResult>> SyncAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>What one rider's turn in the daily run came to.</summary>
public sealed record RiderSyncResult(string RiderId, SyncSummary Summary, string? Error = null);
