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

        return new RideDetail
        {
            Id = ride.Id,
            StartTime = ride.StartTime,
            EndTime = ride.EndTime,
            DistanceKm = Math.Round(ride.DistanceMeters / 1000.0, 1),
            DurationMinutes = Math.Round(ride.Duration.TotalMinutes),
            Sport = ride.Sport,
            Source = ride.Source.ToString(),
            AverageSpeedKmh = ride.AverageSpeedKmh,
            MaximumSpeedKmh = ride.MaximumSpeedKmh,
            AverageHeartRate = ride.AverageHeartRate,
            MaximumHeartRate = ride.MaximumHeartRate,
            ElevationGainMeters = ride.ElevationGainMeters,
            AverageCadence = ride.AverageCadence,
            Calories = ride.Calories,
            RoutePolyline = ride.RoutePolyline,
        };
    }
}
