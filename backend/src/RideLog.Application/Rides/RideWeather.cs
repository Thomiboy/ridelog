namespace RideLog.Application.Rides;

/// <summary>
/// A ride's weather as the detail view needs it, in the two shapes it is read in.
/// </summary>
/// <param name="Hours">What the service reported, hour by hour, for the card.</param>
/// <param name="HeadwindKmhBySample">
/// Headwind at each point of the ride's metric series, aligned to it one for one, for the graph.
/// Per sample rather than per hour because the wind changes by the hour but the rider's direction
/// changes with the road: an hourly figure calls the hour a rider turned for home neither one thing
/// nor the other, and buries a run home that was pushed all the way.
/// </param>
public sealed record RideWeather(
    IReadOnlyList<WeatherHour> Hours,
    IReadOnlyList<double?> HeadwindKmhBySample);
