using Microsoft.EntityFrameworkCore;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

internal sealed class GetRideRoutesQueryHandler(RideLogDbContext context)
    : IQueryHandler<GetRideRoutesQuery, IReadOnlyList<RideRoute>>
{
    public async Task<IReadOnlyList<RideRoute>> HandleAsync(
        GetRideRoutesQuery query, CancellationToken cancellationToken = default)
    {
        var cycling = context.Rides.Where(ride => ride.RoutePolyline != null);
        foreach (var keyword in CyclingRides.NonCyclingKeywords)
        {
            // Exclude known non-cycling sports; untagged rides ("Unknown") pass through.
            cycling = cycling.Where(ride => !ride.Sport.ToLower().Contains(keyword));
        }

        var rows = await cycling
            .Select(ride => new { ride.Id, ride.StartTime, ride.RoutePolyline })
            .ToListAsync(cancellationToken);

        // Ordered in memory (SQLite can't ORDER BY DateTimeOffset); newest first for a stable feed.
        return rows
            .OrderByDescending(row => row.StartTime)
            .Select(row => new RideRoute(row.Id, row.RoutePolyline!))
            .ToList();
    }
}
