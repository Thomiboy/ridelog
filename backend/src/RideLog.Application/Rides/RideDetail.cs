namespace RideLog.Application.Rides;

/// <summary>Full detail for one ride: display-ready metrics, the source badge, and the encoded route.</summary>
public sealed record RideDetail
{
    public required Guid Id { get; init; }
    public required DateTimeOffset StartTime { get; init; }
    public required DateTimeOffset EndTime { get; init; }
    public required double DistanceKm { get; init; }
    public required double DurationMinutes { get; init; }
    public required string Sport { get; init; }

    /// <summary>Source chips (tokens: PolarAutoSync / PolarImport / Bryton), localized on the client.</summary>
    public required IReadOnlyList<string> Sources { get; init; }

    public double? AverageSpeedKmh { get; init; }
    public double? MaximumSpeedKmh { get; init; }
    public int? AverageHeartRate { get; init; }
    public int? MaximumHeartRate { get; init; }
    public double? ElevationGainMeters { get; init; }
    public int? AverageCadence { get; init; }
    public int? Calories { get; init; }

    /// <summary>The older neighbour in the cycling list (earlier in time); null at the oldest ride.</summary>
    public Guid? PreviousId { get; init; }

    /// <summary>The newer neighbour in the cycling list (later in time); null at the newest ride.</summary>
    public Guid? NextId { get; init; }

    public string? RoutePolyline { get; init; }
}
