namespace RideLog.Domain.Rides;

/// <summary>Where a ride record originated. Bryton FIT uploads enrich an existing ride, so they are not a source.</summary>
public enum RideSource
{
    /// <summary>Automatic sync from the Polar AccessLink API.</summary>
    Polar,

    /// <summary>One-time historical GPX/TCX bulk import.</summary>
    Import,
}
