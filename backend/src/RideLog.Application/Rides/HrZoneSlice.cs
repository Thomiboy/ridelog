namespace RideLog.Application.Rides;

/// <summary>Time spent in one heart-rate zone (1–5). Minutes are zero when the zone wasn't reached.</summary>
public sealed record HrZoneSlice(int Zone, double Minutes);
