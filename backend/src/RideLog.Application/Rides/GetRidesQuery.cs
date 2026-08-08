using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>The public, paged cycling-ride list, newest first.</summary>
/// <param name="RiderId">
/// Whose log this is asking about. Named on the query rather than resolved from an ambient service:
/// a parameter cannot be forgotten, and nothing saying whose data it wanted is exactly what let six
/// handlers read the whole table unnoticed (docs/adr/0006).
/// </param>
public sealed record GetRidesQuery(string RiderId, int Page = 1, int PageSize = 20) : IQuery<PagedResult<RideListItem>>;
