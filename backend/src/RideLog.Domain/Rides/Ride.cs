namespace RideLog.Domain.Rides;

/// <summary>
/// A single recorded activity. All sports are stored raw (the UI filters to cycling);
/// the route is a downsampled encoded polyline column, not row-per-point storage.
/// </summary>
public class Ride
{
    public required Guid Id { get; init; }

    /// <summary>Owner. Single-user today, but every user-owned entity carries it (multi-user-ready schema).</summary>
    public required string UserId { get; init; }

    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }

    public required double DistanceMeters { get; set; }

    /// <summary>Moving duration as reported by the source; can be shorter than EndTime - StartTime.</summary>
    public required TimeSpan Duration { get; set; }

    public double? AverageSpeedKmh { get; set; }
    public double? MaximumSpeedKmh { get; set; }
    public int? AverageHeartRate { get; set; }
    public int? MaximumHeartRate { get; set; }
    public double? ElevationGainMeters { get; set; }
    public int? AverageCadence { get; set; }
    public int? Calories { get; set; }

    /// <summary>Sport type exactly as the source reports it (e.g. Polar "ROAD_CYCLING").</summary>
    public required string Sport { get; set; }

    public required RideSource Source { get; init; }

    /// <summary>Downsampled route as an encoded polyline; null for rides without GPS data.</summary>
    public string? RoutePolyline { get; set; }

    /// <summary>Original payloads behind this ride — e.g. a Polar export plus a merged Bryton FIT.</summary>
    public ICollection<RawFile> RawFiles { get; } = [];

    /// <summary>
    /// Time-overlap matching contract: a manually uploaded file (Bryton FIT) belongs to this ride
    /// when their recording windows intersect for the same user. Together with the unique
    /// (UserId, StartTime) index this prevents the same ride arriving twice from two sources.
    /// Boundaries touching (back-to-back rides) do not overlap.
    /// </summary>
    public bool Overlaps(Ride other)
        => UserId == other.UserId
           && RideOverlap.Intersects(StartTime, EndTime, other.StartTime, other.EndTime);
}
