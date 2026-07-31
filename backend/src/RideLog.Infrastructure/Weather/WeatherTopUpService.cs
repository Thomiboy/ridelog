using Microsoft.EntityFrameworkCore;
using RideLog.Application.Routes;
using RideLog.Application.Weather;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Weather;

/// <summary>Counts from one top-up run, one per outcome the lookups produced.</summary>
public sealed record WeatherTopUpSummary(int Fetched, int Unavailable, int Failed);

/// <summary>
/// Fills in weather for rides that have none, a bounded batch at a time. Deliberately separate from
/// import: the import transaction commits even when an exercise fails, so a third-party call inside
/// it would turn any weather outage into a lost ride (docs/adr/0005). A ride with no weather can be
/// topped up tomorrow; a ride that never imported cannot.
/// </summary>
public sealed class WeatherTopUpService(RideLogDbContext context, IWeatherProvider provider)
{
    public async Task<WeatherTopUpSummary> TopUpAsync(
        string userId, int max, CancellationToken cancellationToken = default)
    {
        // Ordered and bounded client-side: SQLite (the test database) cannot ORDER BY a
        // DateTimeOffset, so the read side of this codebase materialises first throughout.
        // Never tried, or tried and failed in a way that might not recur. A ride already carrying
        // weather needs nothing, and one the service will never cover must not be asked again —
        // that is the whole reason the outcome is stored (docs/adr/0005).
        var candidates = (await context.Rides
                .Where(ride => ride.UserId == userId
                               && ride.RoutePolyline != null
                               && (ride.WeatherOutcome == null || ride.WeatherOutcome == WeatherOutcome.Failed))
                .ToListAsync(cancellationToken))
            .OrderByDescending(ride => ride.StartTime)
            .Take(max)
            .ToList();

        int fetched = 0, unavailable = 0, failed = 0;

        foreach (var ride in candidates)
        {
            var start = PolylineDecoder.Decode(ride.RoutePolyline!)[0];
            var lookup = await provider.GetHourlyAsync(
                start.Latitude, start.Longitude, ride.StartTime, ride.EndTime, cancellationToken);

            ride.WeatherOutcome = lookup.Outcome;
            if (lookup.Outcome == WeatherOutcome.Fetched)
            {
                ride.Weather = lookup.Readings;
                fetched++;
            }
            else if (lookup.Outcome == WeatherOutcome.Unavailable)
            {
                unavailable++;
            }
            else
            {
                failed++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new WeatherTopUpSummary(fetched, unavailable, failed);
    }
}
