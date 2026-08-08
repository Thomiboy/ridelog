using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>The public Statistics aggregates and records for cycling rides.</summary>
public sealed record GetStatisticsQuery(string RiderId) : IQuery<StatisticsResult>;
