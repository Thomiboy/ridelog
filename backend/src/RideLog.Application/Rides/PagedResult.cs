namespace RideLog.Application.Rides;

/// <summary>A page of results plus the paging metadata the UI needs to render controls.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
