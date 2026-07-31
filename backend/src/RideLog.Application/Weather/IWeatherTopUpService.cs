namespace RideLog.Application.Weather;

/// <summary>Counts from one top-up run, one per outcome the lookups produced.</summary>
public sealed record WeatherTopUpSummary(int Fetched, int Unavailable, int Failed);

/// <summary>
/// Fills in weather for rides that have none, a bounded batch at a time. Kept out of import on
/// purpose: the import transaction commits even when an exercise fails, so a third-party call
/// inside it would turn any weather outage into a lost ride (docs/adr/0005).
/// </summary>
public interface IWeatherTopUpService
{
    /// <summary>
    /// Looks up at most <paramref name="max"/> rides that have no weather yet, newest first.
    /// </summary>
    Task<WeatherTopUpSummary> TopUpAsync(string userId, int max, CancellationToken cancellationToken = default);
}
