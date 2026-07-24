using RideLog.Application.Users;
using RideLog.Domain.Users;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Users;

internal sealed class UserSettingsService(RideLogDbContext context) : IUserSettingsService
{
    public async Task<UserSettingsDto> GetAsync(string userId, CancellationToken cancellationToken = default)
    {
        var settings = await context.UserSettings.FindAsync([userId], cancellationToken);
        return new UserSettingsDto(settings?.MaxHeartRate);
    }

    public async Task SetMaxHeartRateAsync(string userId, int? maxHeartRate, CancellationToken cancellationToken = default)
    {
        var settings = await context.UserSettings.FindAsync([userId], cancellationToken);
        if (settings is null)
        {
            settings = new UserSettings { UserId = userId };
            context.UserSettings.Add(settings);
        }

        settings.MaxHeartRate = maxHeartRate;
        await context.SaveChangesAsync(cancellationToken);
    }
}
