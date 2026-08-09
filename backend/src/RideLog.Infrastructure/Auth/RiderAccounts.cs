using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RideLog.Application.Auth;
using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Auth;

internal sealed class RiderAccounts(
    RideLogDbContext context,
    UserManager<Rider> users,
    IRideMaintenanceService maintenance,
    IOptions<PublicLogOptions> publicLog) : IRiderAccounts
{
    public async Task<IReadOnlyList<RiderSummary>> ListAsync(CancellationToken cancellationToken = default)
    {
        var publicRider = publicLog.Value.RiderId;

        return
        // Both figures are grouped in the database — the byte sum becomes SUM(DATALENGTH(...)), which
        // matters: pulling raw files into memory to measure them is how a 512 MB instance dies.
        await users.Users
            .OrderBy(rider => rider.Email)
            .Select(rider => new RiderSummary(
                rider.Id,
                rider.Email ?? string.Empty,
                rider.Approval,
                context.Rides.Count(ride => ride.UserId == rider.Id),
                context.Set<RawFile>()
                    .Where(file => file.UserId == rider.Id)
                    .Sum(file => (long)file.Content.Length),
                context.PolarConnections.Any(link => link.UserId == rider.Id),
                // A rider has at most one link (the unique index says so), so this is that one's
                // reading rather than an aggregate — which SQLite cannot do over DateTimeOffset.
                context.PolarConnections
                    .Where(link => link.UserId == rider.Id)
                    .Select(link => link.LastSyncAt)
                    .FirstOrDefault(),
                rider.Id == publicRider))
            .ToListAsync(cancellationToken);
    }

    public async Task<ApprovalChange> SetApprovalAsync(
        string actingRiderId, string riderId, Approval approval, CancellationToken cancellationToken = default)
    {
        // Both refusals are checked before anything changes, so a refusal leaves the rider exactly
        // as they were. Letting somebody in is always safe; only shutting them out can strand you.
        if (approval != Approval.Approved)
        {
            if (riderId == actingRiderId)
            {
                return ApprovalChange.RefusedSelf;
            }

            if (riderId == publicLog.Value.RiderId)
            {
                return ApprovalChange.RefusedPublicLog;
            }
        }

        var rider = await users.FindByIdAsync(riderId);
        if (rider is null)
        {
            return ApprovalChange.UnknownRider;
        }

        rider.Approval = approval;
        await users.UpdateAsync(rider);

        return ApprovalChange.Changed;
    }

    public async Task<bool> SetPublicLogAsync(string riderId, CancellationToken cancellationToken = default)
    {
        // A rider who is not in cannot be the face of the site: their sync is stopped and they
        // cannot sign in to tend the log a visitor would be looking at.
        var rider = await users.FindByIdAsync(riderId);
        if (rider is null || rider.Approval != Approval.Approved)
        {
            return false;
        }

        publicLog.Value.RiderId = riderId;
        return true;
    }

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
