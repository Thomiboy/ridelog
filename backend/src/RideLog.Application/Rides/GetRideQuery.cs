using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>One ride's full detail, or null when it does not exist.</summary>
public sealed record GetRideQuery(Guid Id) : IQuery<RideDetail?>;
