using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>The public dashboard aggregates for cycling rides.</summary>
public sealed record GetDashboardQuery : IQuery<DashboardStats>;
