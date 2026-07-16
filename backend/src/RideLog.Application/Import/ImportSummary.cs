namespace RideLog.Application.Import;

/// <summary>What happened to a single uploaded file.</summary>
public enum ImportOutcome
{
    /// <summary>Parsed and stored as a new ride.</summary>
    Imported,

    /// <summary>A ride covering the same time range already exists (dedup).</summary>
    Skipped,

    /// <summary>The file could not be parsed or stored.</summary>
    Failed,
}

/// <summary>Per-file outcome of an import.</summary>
public sealed record FileImportResult(string FileName, ImportOutcome Outcome, string? Error = null, Guid? RideId = null);

/// <summary>The result of importing a batch of files, one entry per file in upload order.</summary>
public sealed record ImportSummary(IReadOnlyList<FileImportResult> Files)
{
    public int Imported => Files.Count(f => f.Outcome == ImportOutcome.Imported);
    public int Skipped => Files.Count(f => f.Outcome == ImportOutcome.Skipped);
    public int Failed => Files.Count(f => f.Outcome == ImportOutcome.Failed);
}
