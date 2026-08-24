namespace RideLog.Application.Settings;

/// <summary>
/// The keys the <see cref="ISettingsStore"/> holds. Owned by code, not by users, so they live in one
/// place — #168's contact kill switch adds the next one here.
/// </summary>
public static class SettingsKeys
{
    /// <summary>Which rider's log a signed-out visitor sees; mirrors <c>PublicLogOptions.RiderId</c>.</summary>
    public const string PublicLogRiderId = "public-log-rider-id";

    /// <summary>The contact form's kill switch (#168). Stored so the owner can flip it without a restart; unset means on.</summary>
    public const string ContactFormEnabled = "contact-form-enabled";
}
