using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>Every cycling route (with a stored polyline) for the all-routes coverage map.</summary>
public sealed record GetRideRoutesQuery : IQuery<IReadOnlyList<RideRoute>>;
