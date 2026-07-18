namespace RideLog.Application.Rides;

/// <summary>Counts from a reprocess run: how many rides were re-parsed and how many failed.</summary>
public sealed record ReprocessSummary(int Processed, int Failed);

/// <summary>
/// Admin maintenance over already-stored rides. Reprocessing re-parses each ride's original
/// raw files (the only way to fix Polar-synced rides, which AccessLink never re-serves) and
/// updates its metrics in place; deleting removes every ride and its raw files for a user.
/// </summary>
public interface IRideMaintenanceService
{
    Task<ReprocessSummary> ReprocessAsync(string userId, CancellationToken cancellationToken = default);

    Task<int> DeleteAllAsync(string userId, CancellationToken cancellationToken = default);
}
