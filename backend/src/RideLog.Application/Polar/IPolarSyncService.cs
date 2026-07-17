namespace RideLog.Application.Polar;

/// <summary>Pulls new Polar exercises and lands them as rides, idempotently.</summary>
public interface IPolarSyncService
{
    Task<SyncSummary> SyncAsync(string userId, CancellationToken cancellationToken = default);
}
