namespace RideLog.Application.Settings;

/// <summary>
/// The keys the <see cref="ISettingsStore"/> holds. Owned by code, not by users, so they live in one
/// place — #168's contact kill switch adds the next one here.
/// </summary>
public static class SettingsKeys
{
    /// <summary>Which rider's log a signed-out visitor sees; mirrors <c>PublicLogOptions.RiderId</c>.</summary>
    public const string PublicLogRiderId = "public-log-rider-id";
}
