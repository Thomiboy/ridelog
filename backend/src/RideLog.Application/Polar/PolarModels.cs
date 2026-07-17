namespace RideLog.Application.Polar;

/// <summary>An open AccessLink exercise transaction: a snapshot of new exercises to pull, then acknowledge.</summary>
public sealed record PolarTransaction(string Id, IReadOnlyList<string> ExerciseUrls);

/// <summary>Summary metadata for one Polar exercise (the detail before GPX/TCX are downloaded).</summary>
public sealed record PolarExercise(string ExerciseUrl, DateTimeOffset StartTime, string Sport);

/// <summary>An AccessLink OAuth2 access token plus the Polar member id it belongs to. AccessLink tokens do not expire.</summary>
public sealed record PolarToken(string AccessToken, string PolarUserId);

/// <summary>A stored Polar connection: which app user linked which Polar account.</summary>
public sealed record PolarConnectionInfo(string AppUserId, PolarToken Token);

/// <summary>Outcome of a sync run.</summary>
public sealed record SyncSummary(int Imported, int Skipped, int Failed);

/// <summary>Whether a Polar account is linked, since when, and when the last sync ran.</summary>
public sealed record PolarStatus(bool Linked, DateTimeOffset? ConnectedAt, DateTimeOffset? LastSyncAt);
