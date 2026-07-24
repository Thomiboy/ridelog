namespace RideLog.Application.Rides;

/// <summary>
/// A long cycling ride reduced to what the background map needs: its identity, date, distance and
/// the encoded route polyline. Only rides that actually carry a route are returned.
/// </summary>
public sealed record LongestRideRoute(Guid Id, DateTimeOffset Date, double DistanceKm, string RoutePolyline);
