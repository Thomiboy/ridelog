namespace RideLog.Infrastructure.Persistence;

/// <summary>
/// One app-wide setting, stored as a key/value row so a change the owner makes at runtime survives a
/// restart. A persistence concern rather than a domain concept — nothing in the domain reasons about
/// it — so it lives here beside the other rows rather than in Domain.
/// </summary>
internal sealed class AppSetting
{
    public required string Key { get; init; }

    public string Value { get; set; } = string.Empty;
}
