using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideLog.Application.Auth;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Auth;

internal sealed class RiderAccounts(
    RideLogDbContext context,
    UserManager<IdentityUser> users,
    IRideMaintenanceService maintenance,
    IOptions<PublicLogOptions> publicLog) : IRiderAccounts
{
    public async Task<AccountClosure> CloseAsync(string riderId, CancellationToken cancellationToken = default)
    {
        // Checked before anything is removed: a refusal has to leave the rider exactly as they were.
        if (riderId == publicLog.Value.RiderId)
        {
            return AccountClosure.RefusedPublicLog;
        }

        var rider = await users.FindByIdAsync(riderId);
        if (rider is null)
        {
            return AccountClosure.UnknownRider;
        }

        // The rides go through maintenance rather than a delete here, so leaving disposes of raw
        // files by the same route that "delete all my rides" does.
        await maintenance.DeleteAllAsync(riderId, cancellationToken);

        await context.PolarConnections
            .Where(link => link.UserId == riderId)
            .ExecuteDeleteAsync(cancellationToken);
        await context.UserSettings
            .Where(settings => settings.UserId == riderId)
            .ExecuteDeleteAsync(cancellationToken);

        // Last, so a failure anywhere above leaves a rider who can still sign in and try again.
        await users.DeleteAsync(rider);

        return AccountClosure.Closed;
    }
}
