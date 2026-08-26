using RideLog.Application.Messaging;

namespace RideLog.Application.Analysis;

/// <summary>
/// Everything one rider's month has to say, gathered for an analysis to be written from it (#187).
/// Named for the rider it is for, like every other read here (docs/adr/0006).
/// </summary>
public sealed record GetMonthlyTrainingSummaryQuery(string RiderId, int Year, int Month)
    : IQuery<MonthlyTrainingSummary>;
