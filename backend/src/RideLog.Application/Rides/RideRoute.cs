namespace RideLog.Application.Rides;

/// <summary>
/// A ride reduced to what the all-routes coverage map needs: its identity and encoded route
/// polyline. Only rides that actually carry a route are returned.
/// </summary>
public sealed record RideRoute(Guid Id, string RoutePolyline);
