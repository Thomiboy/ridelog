using Microsoft.EntityFrameworkCore;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

internal sealed class GetRidesQueryHandler(RideLogDbContext context)
    : IQueryHandler<GetRidesQuery, PagedResult<RideListItem>>
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<RideListItem>> HandleAsync(GetRidesQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, MaxPageSize);

        var cycling = context.Rides.AsQueryable();
        foreach (var keyword in CyclingRides.NonCyclingKeywords)
        {
            // Exclude known non-cycling sports; untagged rides ("Unknown") pass through.
            cycling = cycling.Where(ride => !ride.Sport.ToLower().Contains(keyword));
        }

        var total = await cycling.CountAsync(cancellationToken);

        // Project to a lightweight summary server-side (no RawFile blobs), then order and page in
        // memory: SQLite can't ORDER BY DateTimeOffset, and at single-user scale the row set is small.
        var rows = await cycling
            .Select(ride => new
            {
                ride.Id,
                ride.StartTime,
                ride.DistanceMeters,
                ride.Duration,
                ride.AverageSpeedKmh,
                ride.ElevationGainMeters,
                ride.Sport,
                ride.Source,
                Formats = ride.RawFiles.Select(f => f.Format).ToList(),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .OrderByDescending(row => row.StartTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new RideListItem
        {
            Id = row.Id,
            StartTime = row.StartTime,
            DistanceKm = Math.Round(row.DistanceMeters / 1000.0, 1),
            DurationMinutes = Math.Round(row.Duration.TotalMinutes),
            AverageSpeedKmh = row.AverageSpeedKmh,
            ElevationGainMeters = row.ElevationGainMeters,
            Sport = row.Sport,
            Sources = RideSourceLabels.Derive(row.Source, row.Formats),
        }).ToList();

        return new PagedResult<RideListItem>(items, page, pageSize, total);
    }
}
