using Microsoft.EntityFrameworkCore;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

internal sealed class GetLongestRidesQueryHandler(RideLogDbContext context)
    : IQueryHandler<GetLongestRidesQuery, IReadOnlyList<LongestRideRoute>>
{
    private const int MaxTake = 10;

    public async Task<IReadOnlyList<LongestRideRoute>> HandleAsync(
        GetLongestRidesQuery query, CancellationToken cancellationToken = default)
    {
        var take = Math.Clamp(query.Take <= 0 ? 3 : query.Take, 1, MaxTake);

        var cycling = context.Rides.Where(ride => ride.RoutePolyline != null);
        foreach (var keyword in CyclingRides.NonCyclingKeywords)
        {
            // Exclude known non-cycling sports; untagged rides ("Unknown") pass through.
            cycling = cycling.Where(ride => !ride.Sport.ToLower().Contains(keyword));
        }

        // Project a lightweight row server-side (no RawFile blobs), then order by distance in memory:
        // ordering happens in memory to match the other handlers, and the row set is small.
        var rows = await cycling
            .Select(ride => new { ride.Id, ride.StartTime, ride.DistanceMeters, ride.RoutePolyline })
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(row => row.DistanceMeters)
            .Take(take)
            .Select(row => new LongestRideRoute(
                row.Id,
                row.StartTime,
                Math.Round(row.DistanceMeters / 1000.0, 1),
                row.RoutePolyline!))
            .ToList();
    }
}
