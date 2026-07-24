namespace RideLog.Application.Users;

/// <summary>The user's preferences as exposed to the client.</summary>
public sealed record UserSettingsDto(int? MaxHeartRate);

/// <summary>Reads and updates a user's settings (currently just the max heart rate).</summary>
public interface IUserSettingsService
{
    Task<UserSettingsDto> GetAsync(string userId, CancellationToken cancellationToken = default);

    Task SetMaxHeartRateAsync(string userId, int? maxHeartRate, CancellationToken cancellationToken = default);
}
