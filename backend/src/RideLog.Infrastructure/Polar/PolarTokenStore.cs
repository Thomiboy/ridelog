using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RideLog.Application.Polar;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Polar;

internal sealed class PolarTokenStore : IPolarTokenStore
{
    private const string Purpose = "Polar.AccessToken";

    private readonly RideLogDbContext _context;
    private readonly IDataProtector _protector;

    public PolarTokenStore(RideLogDbContext context, IDataProtectionProvider protectionProvider)
    {
        _context = context;
        _protector = protectionProvider.CreateProtector(Purpose);
    }

    public async Task SaveAsync(string appUserId, PolarToken token, CancellationToken cancellationToken = default)
    {
        var protectedToken = _protector.Protect(token.AccessToken);

        var connection = await _context.PolarConnections
            .SingleOrDefaultAsync(c => c.UserId == appUserId, cancellationToken);

        if (connection is null)
        {
            _context.PolarConnections.Add(new PolarConnection
            {
                Id = Guid.NewGuid(),
                UserId = appUserId,
                PolarUserId = token.PolarUserId,
                AccessTokenProtected = protectedToken,
                ConnectedAt = DateTimeOffset.UtcNow,
            });
        }
        else
        {
            connection.PolarUserId = token.PolarUserId;
            connection.AccessTokenProtected = protectedToken;
            connection.ConnectedAt = DateTimeOffset.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PolarConnectionInfo?> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        // Single connection in the MVP; the unique UserId index keeps it to one row.
        var connection = await _context.PolarConnections.FirstOrDefaultAsync(cancellationToken);

        if (connection is null)
        {
            return null;
        }

        var accessToken = _protector.Unprotect(connection.AccessTokenProtected);
        return new PolarConnectionInfo(connection.UserId, new PolarToken(accessToken, connection.PolarUserId));
    }

    public async Task<PolarStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var connection = await _context.PolarConnections.FirstOrDefaultAsync(cancellationToken);
        if (connection is null)
        {
            return new PolarStatus(false, null, null, null);
        }

        var lastResult = connection.LastSyncImported is { } imported
            ? new SyncSummary(imported, connection.LastSyncSkipped ?? 0, connection.LastSyncFailed ?? 0)
            : null;
        return new PolarStatus(true, connection.ConnectedAt, connection.LastSyncAt, lastResult);
    }
}
