using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>The public, paged cycling-ride list, newest first.</summary>
public sealed record GetRidesQuery(int Page = 1, int PageSize = 20) : IQuery<PagedResult<RideListItem>>;
