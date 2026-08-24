using Microsoft.EntityFrameworkCore;
using RideLog.Application.Settings;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Settings;

internal sealed class SettingsStore(RideLogDbContext context) : ISettingsStore
{
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => (await context.Set<AppSetting>().FindAsync([key], cancellationToken))?.Value;

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        var setting = await context.Set<AppSetting>().FindAsync([key], cancellationToken);
        if (setting is null)
        {
            setting = new AppSetting { Key = key };
            context.Set<AppSetting>().Add(setting);
        }

        setting.Value = value;
        await context.SaveChangesAsync(cancellationToken);
    }
}
