using Microsoft.EntityFrameworkCore;
using RideLog.Application.Import;
using RideLog.Application.Rides;
using RideLog.Application.Routes;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Rides;

/// <summary>
/// Reprocesses and deletes stored rides. Reprocessing re-parses each ride's raw files with the
/// current parsers and refreshes the metric columns in place — the fix for rides (including
/// Polar-synced ones) that were stored before a parser improvement. Identity, source, timestamps,
/// sport and the raw files themselves are preserved.
/// </summary>
internal sealed class RideMaintenanceService(
    RideLogDbContext context,
    IEnumerable<IActivityFileParser> parsers) : IRideMaintenanceService
{
    // Matches the import/sync route cap so a reprocessed polyline stays the same size.
    private const int MaxRoutePoints = 1000;

    public async Task<ReprocessSummary> ReprocessAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rides = await context.Rides
            .Where(r => r.UserId == userId)
            .Include(r => r.RawFiles)
            .ToListAsync(cancellationToken);

        var processed = 0;
        var failed = 0;

        foreach (var ride in rides)
        {
            try
            {
                if (Reprocess(ride))
                {
                    processed++;
                }
            }
            catch
            {
                // A single unparseable ride must not abort the batch.
                failed++;
            }
        }

        await context.SaveChangesAsync(cancellationToken);
        return new ReprocessSummary(processed, failed);
    }

    public async Task<int> DeleteAllAsync(string userId, CancellationToken cancellationToken = default)
    {
        var rides = await context.Rides
            .Where(r => r.UserId == userId)
            .Include(r => r.RawFiles)
            .ToListAsync(cancellationToken);

        context.Rides.RemoveRange(rides);
        await context.SaveChangesAsync(cancellationToken);
        return rides.Count;
    }

    /// <summary>Re-derives metrics from the stored files, mirroring the Polar sync precedence
    /// (TCX metrics, GPX route). Returns false when a ride has no parseable raw file.</summary>
    private bool Reprocess(Ride ride)
    {
        var tcx = ParseFirst(ride, RawFileFormat.Tcx);
        var gpx = ParseFirst(ride, RawFileFormat.Gpx);
        var metrics = tcx ?? gpx;
        if (metrics is null)
        {
            return false;
        }

        var route = gpx?.RoutePoints ?? tcx?.RoutePoints ?? [];

        ride.DistanceMeters = metrics.DistanceMeters;
        ride.Duration = metrics.Duration;
        ride.AverageSpeedKmh = metrics.AverageSpeedKmh;
        ride.MaximumSpeedKmh = metrics.MaximumSpeedKmh;
        ride.AverageHeartRate = metrics.AverageHeartRate;
        ride.MaximumHeartRate = metrics.MaximumHeartRate;
        ride.ElevationGainMeters = metrics.ElevationGainMeters;
        ride.AverageCadence = metrics.AverageCadence;
        ride.Calories = metrics.Calories;
        ride.RoutePolyline = PolylineEncoder.Encode(Downsample(route));
        // Sport, Source, StartTime, EndTime and the raw files are intentionally left untouched.
        return true;
    }

    private ParsedActivity? ParseFirst(Ride ride, RawFileFormat format)
    {
        var file = ride.RawFiles.FirstOrDefault(f => f.Format == format);
        if (file is null)
        {
            return null;
        }

        var fileName = format == RawFileFormat.Tcx ? "exercise.tcx" : "exercise.gpx";
        var parser = parsers.First(p => p.CanParse(fileName));
        using var stream = new MemoryStream(file.Content);
        return parser.Parse(stream, fileName);
    }

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
