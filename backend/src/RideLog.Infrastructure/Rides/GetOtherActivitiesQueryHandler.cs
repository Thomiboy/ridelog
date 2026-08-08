using Microsoft.EntityFrameworkCore;
using RideLog.Application.Messaging;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

internal sealed class GetOtherActivitiesQueryHandler(RideLogDbContext context)
    : IQueryHandler<GetOtherActivitiesQuery, PagedResult<RideListItem>>
{
    private const int MaxPageSize = 100;

    public async Task<PagedResult<RideListItem>> HandleAsync(
        GetOtherActivitiesQuery query, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, MaxPageSize);

        // Projected server-side, then read for sport in memory: the category is a plain function of
        // the raw name and EF has no way to translate it. The read side already materialises here to
        // order and page, so this costs nothing extra.
        var rows = await context.Rides
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

        var others = rows
            .Where(row => SportCategories.Of(row.Sport) != SportCategory.Cycling)
            .OrderByDescending(row => row.StartTime)
            .ToList();

        var items = others
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
            })
            .ToList();

        return new PagedResult<RideListItem>(items, page, pageSize, others.Count);
    }
}
