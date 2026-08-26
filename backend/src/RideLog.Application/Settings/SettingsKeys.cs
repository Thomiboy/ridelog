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

    /// <summary>
    /// The monthly analysis's kill switch (#187). Unset means <em>off</em> — the opposite of the
    /// contact form's, because that costs nothing and this spends money: a feature that starts
    /// spending because nobody turned it off is the wrong default.
    /// </summary>
    public const string MonthlyAnalysisEnabled = "monthly-analysis-enabled";
}
