namespace RideLog.Infrastructure.Persistence;

/// <summary>
/// A linked Polar account. The access token is stored encrypted (Data Protection); one row per
/// app user (single connection in the MVP, but keyed by UserId for the multi-user-ready schema).
/// </summary>
public class PolarConnection
{
    public required Guid Id { get; init; }

    public required string UserId { get; set; }

    /// <summary>The Polar member id (x_user_id) used to build AccessLink URLs.</summary>
    public required string PolarUserId { get; set; }

    /// <summary>The Data Protection-encrypted access token — never the plaintext.</summary>
    public required string AccessTokenProtected { get; set; }

    public required DateTimeOffset ConnectedAt { get; set; }

    /// <summary>When the last sync run completed; null until the first sync.</summary>
    public DateTimeOffset? LastSyncAt { get; set; }

    /// <summary>Outcome of the most recent sync (automatic or manual); null until the first sync.</summary>
    public int? LastSyncImported { get; set; }
    public int? LastSyncSkipped { get; set; }
    public int? LastSyncFailed { get; set; }
}
