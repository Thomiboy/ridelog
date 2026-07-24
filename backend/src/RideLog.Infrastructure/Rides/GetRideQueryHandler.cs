using Microsoft.EntityFrameworkCore;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

internal sealed class GetRideQueryHandler(RideLogDbContext context)
    : IQueryHandler<GetRideQuery, RideDetail?>
{
    public async Task<RideDetail?> HandleAsync(GetRideQuery query, CancellationToken cancellationToken = default)
    {
        var ride = await context.Rides
            .Where(r => r.Id == query.Id)
            .Select(r => new
            {
                r.Id,
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
                r.RoutePolyline,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (ride is null)
        {
            return null;
        }

        // Chronological neighbours within the cycling set the list shows. Ordered in memory because
        // SQLite can't ORDER BY DateTimeOffset; at single-user scale the row set is small.
        var cycling = context.Rides.AsQueryable();
        foreach (var keyword in CyclingRides.NonCyclingKeywords)
        {
            cycling = cycling.Where(r => !r.Sport.ToLower().Contains(keyword));
        }

        var ordered = (await cycling.Select(r => new { r.Id, r.StartTime }).ToListAsync(cancellationToken))
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
            Sources = RideSourceLabels.Derive(ride.Source, ride.Formats),
            AverageSpeedKmh = ride.AverageSpeedKmh,
            MaximumSpeedKmh = ride.MaximumSpeedKmh,
            AverageHeartRate = ride.AverageHeartRate,
            MaximumHeartRate = ride.MaximumHeartRate,
            ElevationGainMeters = ride.ElevationGainMeters,
            AverageCadence = ride.AverageCadence,
            Calories = ride.Calories,
            PreviousId = previousId,
            NextId = nextId,
            RoutePolyline = ride.RoutePolyline,
        };
    }
}
