namespace RideLog.Application.Import;

/// <summary>Imports uploaded GPX/TCX files as rides, skipping duplicates and reporting per-file results.</summary>
public interface IActivityImporter
{
    Task<ImportSummary> ImportAsync(
        IReadOnlyCollection<ActivityFile> files,
        string userId,
        CancellationToken cancellationToken = default);
}
