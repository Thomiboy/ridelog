using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>One ride's full detail, or null when it does not exist.</summary>
/// <param name="RiderId">Whose log this ride has to belong to; a ride outside it is not found.</param>
public sealed record GetRideQuery(Guid Id, string RiderId) : IQuery<RideDetail?>;
