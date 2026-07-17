using Microsoft.EntityFrameworkCore;
using RideLog.Application.Import;
using RideLog.Application.Polar;
using RideLog.Application.Routes;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Polar;

/// <summary>
/// Pulls new Polar exercises via a transaction, maps each (GPX route + TCX metrics) to a ride,
/// dedups by time overlap, and acknowledges the transaction. One bad exercise never blocks the rest.
/// </summary>
internal sealed class PolarSyncService(
    IPolarClient client,
    RideLogDbContext context,
    IEnumerable<IActivityFileParser> parsers) : IPolarSyncService
{
    private const int MaxRoutePoints = 1000;

    public async Task<SyncSummary> SyncAsync(string userId, CancellationToken cancellationToken = default)
    {
        var transaction = await client.StartTransactionAsync(cancellationToken);
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
                switch (await ImportExerciseAsync(exerciseUrl, userId, cancellationToken))
                {
                    case ImportOutcome.Imported: imported++; break;
                    case ImportOutcome.Skipped: skipped++; break;
                    default: failed++; break;
                }
            }
            catch
            {
                // Resilience: a single failed exercise must not block the rest or the commit.
                failed++;
            }
        }

        // Acknowledge only after processing, so a crash mid-run re-serves the exercises next time.
        await client.CommitTransactionAsync(transaction, cancellationToken);

        await StampLastSyncAsync(userId, cancellationToken);

        return new SyncSummary(imported, skipped, failed);
    }

    private async Task StampLastSyncAsync(string userId, CancellationToken cancellationToken)
    {
        var connection = await context.PolarConnections
            .SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);
        if (connection is not null)
        {
            connection.LastSyncAt = DateTimeOffset.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<ImportOutcome> ImportExerciseAsync(string exerciseUrl, string userId, CancellationToken cancellationToken)
    {
        var exercise = await client.GetExerciseAsync(exerciseUrl, cancellationToken);
        var gpxBytes = await client.DownloadGpxAsync(exerciseUrl, cancellationToken);
        var tcxBytes = await client.DownloadTcxAsync(exerciseUrl, cancellationToken);

        var tcx = Parse(tcxBytes, "exercise.tcx");
        var gpx = Parse(gpxBytes, "exercise.gpx");
        var metrics = tcx ?? gpx ?? throw new InvalidOperationException($"Exercise '{exerciseUrl}' has no GPX or TCX data.");
        var route = gpx?.RoutePoints ?? tcx?.RoutePoints ?? [];

        var windows = await context.Rides
            .Where(r => r.UserId == userId)
            .Select(r => new { r.StartTime, r.EndTime })
            .ToListAsync(cancellationToken);
        if (windows.Any(w => RideOverlap.Intersects(w.StartTime, w.EndTime, metrics.StartTime, metrics.EndTime)))
        {
            return ImportOutcome.Skipped;
        }

        var ride = new Ride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = metrics.StartTime,
            EndTime = metrics.EndTime,
            Duration = metrics.Duration,
            DistanceMeters = metrics.DistanceMeters,
            AverageSpeedKmh = metrics.AverageSpeedKmh,
            MaximumSpeedKmh = metrics.MaximumSpeedKmh,
            AverageHeartRate = metrics.AverageHeartRate,
            MaximumHeartRate = metrics.MaximumHeartRate,
            ElevationGainMeters = metrics.ElevationGainMeters,
            AverageCadence = metrics.AverageCadence,
            Sport = exercise.Sport, // mapped from Polar metadata, not the TCX label
            Source = RideSource.Polar,
            RoutePolyline = PolylineEncoder.Encode(Downsample(route)),
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
