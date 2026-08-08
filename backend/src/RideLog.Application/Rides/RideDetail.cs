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

    /// <summary>
    /// What that raw sport name reads as. Sent rather than left for the reader to work out, so the
    /// reading of a sport lives in one place and the page can tell which list it belongs to.
    /// </summary>
    public required SportCategory SportCategory { get; init; }

    /// <summary>Source chips (tokens: PolarAutoSync / PolarImport / Bryton), localized on the client.</summary>
    public required IReadOnlyList<string> Sources { get; init; }

    public double? AverageSpeedKmh { get; init; }
    public double? MaximumSpeedKmh { get; init; }
    public int? AverageHeartRate { get; init; }
    public int? MaximumHeartRate { get; init; }
    public double? ElevationGainMeters { get; init; }
    public int? AverageCadence { get; init; }
    public int? Calories { get; init; }

    /// <summary>Ambient temperature summary from a merged Bryton FIT; null when none was recorded.</summary>
    public double? AverageTemperatureCelsius { get; init; }
    public double? MinTemperatureCelsius { get; init; }
    public double? MaxTemperatureCelsius { get; init; }

    /// <summary>The older neighbour in the cycling list (earlier in time); null at the oldest ride.</summary>
    public Guid? PreviousId { get; init; }

    /// <summary>The newer neighbour in the cycling list (later in time); null at the newest ride.</summary>
    public Guid? NextId { get; init; }

    public string? RoutePolyline { get; init; }

    /// <summary>Downsampled per-point series for the elevation/HR graph; null when neither was recorded.</summary>
    public IReadOnlyList<Domain.Rides.MetricSample>? MetricSeries { get; init; }

    /// <summary>Time-in-zone (5 slices); null when no HR series or no configured max heart rate.</summary>
    public IReadOnlyList<HrZoneSlice>? HrZones { get; init; }

    /// <summary>Places on the route where the rider paused for more than about a minute; empty when none.</summary>
    public IReadOnlyList<RestStop> RestStops { get; init; } = [];

    /// <summary>
    /// Weather reported for where and when this ride happened — by the hour for the card, and
    /// resolved against the direction ridden at every sample for the graph. Null when no lookup has
    /// stored any.
    /// </summary>
    public RideWeather? Weather { get; init; }
}
