namespace RideLog.Application.Settings;

/// <summary>
/// A durable key/value store for the handful of app-wide settings the owner changes at runtime —
/// today whose log is public, next #168's contact kill switch. Small on purpose: the hot public
/// endpoints still read the public log from the <c>PublicLogOptions</c> singleton (a property
/// access, not a query); this store is what makes a change to it outlive the process that made it,
/// so the next boot resolves the setting from what was stored rather than from configuration.
/// </summary>
public interface ISettingsStore
{
    /// <summary>The stored value for <paramref name="key"/>, or null if nothing is stored.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Stores <paramref name="value"/> under <paramref name="key"/>, replacing any prior value.</summary>
    Task SetAsync(string key, string value, CancellationToken cancellationToken = default);
}
