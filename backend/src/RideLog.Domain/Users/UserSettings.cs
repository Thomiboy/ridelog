namespace RideLog.Domain.Users;

/// <summary>
/// Per-user preferences. Single-user today (the admin), but keyed by UserId like every user-owned
/// entity. Currently just the maximum heart rate that anchors the HR-zone boundaries.
/// </summary>
public class UserSettings
{
    public required string UserId { get; init; }

    /// <summary>Maximum heart rate; null until the admin sets it. HR-zone floors are % of this.</summary>
    public int? MaxHeartRate { get; set; }
}
