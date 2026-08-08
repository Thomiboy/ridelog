using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RideLog.Application.Import;
using RideLog.Application.Polar;
using RideLog.Application.Routes;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Import;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Polar;

/// <summary>
/// Pulls new Polar exercises via a transaction, maps each (GPX route + TCX metrics) to a ride,
/// dedups by time overlap, and acknowledges the transaction. One bad exercise never blocks the rest.
/// </summary>
internal sealed class PolarSyncService(
    IPolarClient client,
    IPolarTokenStore tokenStore,
    RideLogDbContext context,
    IEnumerable<IActivityFileParser> parsers,
    ILogger<PolarSyncService> logger) : IPolarSyncService
{
    private const int MaxRoutePoints = 1000;

    public async Task<SyncSummary> SyncAsync(string userId, CancellationToken cancellationToken = default)
    {
        // The rider's own link, not "the" link: pulling with somebody else's would land their
        // exercises in this rider's log.
        var connection = await tokenStore.GetConnectionAsync(userId, cancellationToken);
        if (connection is null)
        {
            return new SyncSummary(0, 0, 0);
        }

        var link = connection.Token;
        var transaction = await client.StartTransactionAsync(link, cancellationToken);
        if (transaction is null)
        {
            return new SyncSummary(0, 0, 0);
        }

        var imported = 0;
        var skipped = 0;
        var failed = 0;

        foreach (var exerciseUrl in transaction.ExerciseUrls)
        {
            try
            {
                switch (await ImportExerciseAsync(link, exerciseUrl, userId, cancellationToken))
                {
                    case ImportOutcome.Imported: imported++; break;
                    case ImportOutcome.Skipped: skipped++; break;
                    default: failed++; break;
                }
            }
            catch (Exception ex)
            {
                // Resilience: a single failed exercise must not block the rest or the commit.
                // Log loudly — the transaction is still acknowledged, so a lost exercise must be
                // diagnosable (and recovered via a manual Polar Flow export) instead of vanishing.
                failed++;
                logger.LogError(ex, "Failed to import Polar exercise {ExerciseUrl}.", exerciseUrl);
            }
        }

        // Acknowledge only after processing, so a crash mid-run re-serves the exercises next time.
        await client.CommitTransactionAsync(link, transaction, cancellationToken);

        var summary = new SyncSummary(imported, skipped, failed);
        await StampLastSyncAsync(userId, summary, cancellationToken);

        return summary;
    }

    public async Task<IReadOnlyList<RiderSyncResult>> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<RiderSyncResult>();

        foreach (var riderId in await tokenStore.GetLinkedRidersAsync(cancellationToken))
        {
            try
            {
                results.Add(new RiderSyncResult(riderId, await SyncAsync(riderId, cancellationToken)));
            }
            catch (Exception ex)
            {
                // One rider's expired token or outage is theirs. The run is for everyone who linked,
                // and stopping here would silently cost every rider after them.
                logger.LogError(ex, "The daily Polar sync failed for rider {RiderId}.", riderId);
                results.Add(new RiderSyncResult(riderId, new SyncSummary(0, 0, 0), ex.Message));
            }
        }

        return results;
    }

    private async Task StampLastSyncAsync(string userId, SyncSummary summary, CancellationToken cancellationToken)
    {
        var connection = await context.PolarConnections
            .SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (connection is not null)
        {
            connection.LastSyncAt = DateTimeOffset.UtcNow;
            connection.LastSyncImported = summary.Imported;
            connection.LastSyncSkipped = summary.Skipped;
            connection.LastSyncFailed = summary.Failed;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ImportOutcome> ImportExerciseAsync(
        PolarToken link, string exerciseUrl, string userId, CancellationToken cancellationToken)
    {
        var exercise = await client.GetExerciseAsync(link, exerciseUrl, cancellationToken);
        var gpxBytes = await client.DownloadGpxAsync(link, exerciseUrl, cancellationToken);
        var tcxBytes = await client.DownloadTcxAsync(link, exerciseUrl, cancellationToken);

        var tcx = Parse(tcxBytes, "exercise.tcx");
        var gpx = Parse(gpxBytes, "exercise.gpx");
        var metrics = tcx ?? gpx ?? throw new InvalidOperationException($"Exercise '{exerciseUrl}' has no GPX or TCX data.");
        // Prefer the TCX track (position + elevation + heart rate); fall back to GPX (no HR) only when
        // the TCX has no positioned points. This keeps per-point HR in the graph series.
        var route = tcx?.RoutePoints is { Count: > 0 } tcxRoute ? tcxRoute : gpx?.RoutePoints ?? [];

        var windows = await context.Rides
            .Where(r => r.UserId == userId)
            .Select(r => new { r.StartTime, r.EndTime })
            .ToListAsync(cancellationToken);
        if (windows.Any(w => RideOverlap.Intersects(w.StartTime, w.EndTime, metrics.StartTime, metrics.EndTime)))
        {
            return ImportOutcome.Skipped;
        }

        var topSpeed = SpeedSeries.TopSpeedKmh(route, metrics.DeviceMaximumSpeedKmh);
        var ride = new Ride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = metrics.StartTime,
            EndTime = metrics.EndTime,
            Duration = metrics.Duration,
            DistanceMeters = metrics.DistanceMeters,
            AverageSpeedKmh = metrics.AverageSpeedKmh,
            MaximumSpeedKmh = topSpeed,
            AverageHeartRate = metrics.AverageHeartRate,
            MaximumHeartRate = metrics.MaximumHeartRate,
            ElevationGainMeters = metrics.ElevationGainMeters,
            AverageCadence = metrics.AverageCadence,
            Calories = metrics.Calories,
            Sport = exercise.Sport, // mapped from Polar metadata, not the TCX label
            Source = RideSource.Polar,
            RoutePolyline = PolylineEncoder.Encode(Downsample(route)),
            // Build the elevation/HR graph series at ingest so the chart shows without a reprocess.
            MetricSeries = MetricSeriesBuilder.BuildStorable(route, topSpeed),
        };

        if (gpxBytes is not null)
        {
            ride.RawFiles.Add(NewRawFile(userId, RawFileFormat.Gpx, "exercise.gpx", gpxBytes));
        }
        if (tcxBytes is not null)
        {
            ride.RawFiles.Add(NewRawFile(userId, RawFileFormat.Tcx, "exercise.tcx", tcxBytes));
        }

        context.Rides.Add(ride);
        await context.SaveChangesAsync(cancellationToken);

        return ImportOutcome.Imported;
    }

    private ParsedActivity? Parse(byte[]? bytes, string fileName)
    {
        if (bytes is null)
        {
            return null;
        }

        var parser = parsers.First(p => p.CanParse(fileName));
        using var stream = new MemoryStream(bytes);
        return parser.Parse(stream, fileName);
    }

    private static RawFile NewRawFile(string userId, RawFileFormat format, string fileName, byte[] content) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        Format = format,
        FileName = fileName,
        Content = content,
        UploadedAt = DateTimeOffset.UtcNow,
    };

    private static IReadOnlyList<GeoPoint> Downsample(IReadOnlyList<GeoPoint> points)
    {
        if (points.Count <= MaxRoutePoints)
        {
            return points;
        }

        var stride = (int)Math.Ceiling((double)points.Count / MaxRoutePoints);
        var sampled = new List<GeoPoint>();
        for (var i = 0; i < points.Count; i += stride)
        {
            sampled.Add(points[i]);
        }
        if (!sampled[^1].Equals(points[^1]))
        {
            sampled.Add(points[^1]);
        }

        return sampled;
    }
}
