using Microsoft.EntityFrameworkCore;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Application.Routes;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

internal sealed class GetRideQueryHandler(RideLogDbContext context)
    : IQueryHandler<GetRideQuery, RideDetail?>
{
    public async Task<RideDetail?> HandleAsync(GetRideQuery query, CancellationToken cancellationToken = default)
    {
        var ride = await context.Rides
            .Where(r => r.Id == query.Id && r.UserId == query.RiderId)
            .Select(r => new
            {
                r.Id,
                r.UserId,
                r.StartTime,
                r.EndTime,
                r.DistanceMeters,
                r.Duration,
                r.Sport,
                r.Source,
                Formats = r.RawFiles.Select(f => f.Format).ToList(),
                r.AverageSpeedKmh,
                r.MaximumSpeedKmh,
                r.AverageHeartRate,
                r.MaximumHeartRate,
                r.ElevationGainMeters,
                r.AverageCadence,
                r.Calories,
                r.AverageTemperatureCelsius,
                r.MinTemperatureCelsius,
                r.MaxTemperatureCelsius,
                r.RoutePolyline,
                r.MetricSeries,
                r.Weather,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (ride is null)
        {
            return null;
        }

        // Time-in-zone is computed on read from the stored HR series against the user's max HR,
        // so changing the max HR immediately reflects without reprocessing anything.
        var maxHeartRate = (await context.UserSettings
            .FirstOrDefaultAsync(s => s.UserId == ride.UserId, cancellationToken))?.MaxHeartRate;
        var hrZones = ride.MetricSeries is { } series && maxHeartRate is { } max
            ? HrZoneCalculator.TimeInZone(series, max)
            : null;

        // Rest stops: detected from the series (time stalls, distance holds), placed on the decoded route.
        var restStops = ride.MetricSeries is { } restSeries && ride.RoutePolyline is { } polyline
            ? RestStopDetector.RestStops(restSeries, PolylineDecoder.Decode(polyline))
            : [];

        // Chronological neighbours within the list this recording belongs to — rides step between
        // rides, other activities between other activities. Stepping across the two would walk the
        // reader out of the list they arrived from and into one this recording isn't in.
        //
        // Read in memory because the category is a plain function of the raw sport name and EF cannot
        // translate it; ordering has to happen here anyway, since SQLite can't ORDER BY a
        // DateTimeOffset, and at single-rider scale the row set is small.
        // Membership is the list, not the fine category: the other-activity list holds runs, walks
        // and swims together, so a run steps to the walk beside it. Which run or walk it is matters
        // for reading the page, not for which list the reader is stepping through.
        var isRide = SportCategories.Of(ride.Sport) == SportCategory.Cycling;

        var ordered = (await context.Rides
                .Where(r => r.UserId == query.RiderId)
                .Select(r => new { r.Id, r.StartTime, r.Sport })
                .ToListAsync(cancellationToken))
            .Where(r => (SportCategories.Of(r.Sport) == SportCategory.Cycling) == isRide)
            .OrderBy(r => r.StartTime)
            .ToList();
        var index = ordered.FindIndex(r => r.Id == query.Id);
        var previousId = index > 0 ? ordered[index - 1].Id : (Guid?)null; // older
        var nextId = index >= 0 && index < ordered.Count - 1 ? ordered[index + 1].Id : (Guid?)null; // newer

        return new RideDetail
        {
            Id = ride.Id,
            StartTime = ride.StartTime,
            EndTime = ride.EndTime,
            DistanceKm = Math.Round(ride.DistanceMeters / 1000.0, 1),
            DurationMinutes = Math.Round(ride.Duration.TotalMinutes),
            Sport = ride.Sport,
            SportCategory = SportCategories.Of(ride.Sport),
            Sources = RideSourceLabels.Derive(ride.Source, ride.Formats),
            AverageSpeedKmh = ride.AverageSpeedKmh,
            MaximumSpeedKmh = ride.MaximumSpeedKmh,
            AverageHeartRate = ride.AverageHeartRate,
            MaximumHeartRate = ride.MaximumHeartRate,
            ElevationGainMeters = ride.ElevationGainMeters,
            AverageCadence = ride.AverageCadence,
            Calories = ride.Calories,
            AverageTemperatureCelsius = ride.AverageTemperatureCelsius,
            MinTemperatureCelsius = ride.MinTemperatureCelsius,
            MaxTemperatureCelsius = ride.MaxTemperatureCelsius,
            PreviousId = previousId,
            NextId = nextId,
            RoutePolyline = ride.RoutePolyline,
            MetricSeries = ride.MetricSeries,
            HrZones = hrZones,
            RestStops = restStops,
            Weather = RideWeatherReader.Read(ride.Weather, ride.RoutePolyline, ride.MetricSeries, ride.StartTime),
        };
    }
}
