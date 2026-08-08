using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>
/// The longest cycling routes for the background map, longest first. Only rides with a stored
/// route polyline count; <paramref name="Take"/> caps how many come back (clamped in the handler).
/// </summary>
public sealed record GetLongestRidesQuery(string RiderId, int Take = 3) : IQuery<IReadOnlyList<LongestRideRoute>>;
