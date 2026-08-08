using RideLog.Application.Messaging;

namespace RideLog.Application.Rides;

/// <summary>
/// The recordings that are not rides — runs, walks, swims — newest first. A sibling of
/// <see cref="GetRidesQuery"/> rather than a filter over it: nothing is a term for both, and the
/// rides list is left exactly as it was (docs/adr/0004).
/// </summary>
public sealed record GetOtherActivitiesQuery(int Page = 1, int PageSize = 20)
    : IQuery<PagedResult<RideListItem>>;
