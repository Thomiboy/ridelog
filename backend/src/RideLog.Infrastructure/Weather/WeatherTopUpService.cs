using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RideLog.Application.Routes;
using RideLog.Application.Weather;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Weather;

/// <summary>
/// Fills in weather for rides that have none, a bounded batch at a time. Deliberately separate from
/// import: the import transaction commits even when an exercise fails, so a third-party call inside
/// it would turn any weather outage into a lost ride (docs/adr/0005). A ride with no weather can be
/// topped up tomorrow; a ride that never imported cannot.
/// </summary>
public sealed class WeatherTopUpService(
    RideLogDbContext context,
    IWeatherProvider provider,
    TimeProvider clock,
    ILogger<WeatherTopUpService> logger) : IWeatherTopUpService
{
    /// <summary>
    /// How long an empty answer stays worth retrying. The archive was measured serving complete data
    /// for the previous day, so a week is generous — but "no data" for a ride this recent means the
    /// archive has not caught up, not that it never will, and the two lead to opposite decisions.
    /// </summary>
    private static readonly TimeSpan CatchUpWindow = TimeSpan.FromDays(7);

    public async Task<WeatherTopUpSummary> TopUpAsync(
        string userId, int max, CancellationToken cancellationToken = default)
    {
        // Never tried, or tried and failed in a way that might not recur. A ride already carrying
        // weather needs nothing, and one the service will never cover must not be asked again —
        // that is the whole reason the outcome is stored (docs/adr/0005).
        //
        // A ride with no route has no position to look up, so it is left out rather than asked
        // about: there is nothing a retry could ever change, and skipping costs no call.
        //
        // Newest first, and bounded, so a day's quota goes to the rides most likely to be looked at.
        // Both happen client-side because SQLite (the test database) cannot ORDER BY a
        // DateTimeOffset, matching how the read side of this codebase already works.
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

            // One unreachable service must not cost the batch: a throw is just another failure,
            // recorded on the ride so tomorrow's run picks it up again.
            WeatherLookup lookup;
            try
            {
                lookup = await provider.GetHourlyAsync(
                    start.Latitude, start.Longitude, ride.StartTime, ride.EndTime, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogError(exception, "Weather lookup failed for ride {RideId}", ride.Id);
                lookup = WeatherLookup.Failed;
            }

            var outcome = lookup.Outcome == WeatherOutcome.Unavailable
                          && clock.GetUtcNow() - ride.EndTime < CatchUpWindow
                ? WeatherOutcome.Failed
                : lookup.Outcome;

            ride.WeatherOutcome = outcome;
            if (outcome == WeatherOutcome.Fetched)
            {
                ride.Weather = lookup.Readings;
                fetched++;
            }
            else if (outcome == WeatherOutcome.Unavailable)
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
