using Microsoft.EntityFrameworkCore;
using RideLog.Application.Import;
using RideLog.Application.Routes;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Import;

internal sealed class ActivityImporter(
    RideLogDbContext context,
    IEnumerable<IActivityFileParser> parsers) : IActivityImporter
{
    // Cap the stored route so a dense track stays a compact polyline in the 32 GB free-tier DB.
    private const int MaxRoutePoints = 1000;

    public async Task<ImportSummary> ImportAsync(
        IReadOnlyCollection<ActivityFile> files,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var results = new List<FileImportResult>();

        foreach (var file in files)
        {
            try
            {
                results.Add(await ImportOneAsync(file, userId, cancellationToken));
            }
            catch (Exception ex)
            {
                // One bad file must not abort the batch — record it and move on.
                results.Add(new FileImportResult(file.FileName, ImportOutcome.Failed, ex.Message));
            }
        }

        return new ImportSummary(results);
    }

    private async Task<FileImportResult> ImportOneAsync(ActivityFile file, string userId, CancellationToken cancellationToken)
    {
        var parser = parsers.FirstOrDefault(p => p.CanParse(file.FileName));
        if (parser is null)
        {
            return new FileImportResult(file.FileName, ImportOutcome.Failed, $"No parser for '{file.FileName}'.");
        }

        ParsedActivity parsed;
        using (var stream = new MemoryStream(file.Content))
        {
            parsed = parser.Parse(stream, file.FileName);
        }

        var format = FormatFor(file.FileName);

        // Match by the time-overlap contract: a ride whose window intersects this file's window
        // (for this user) is the same ride. Windows are checked in memory so the rule works
        // identically on SQL Server and SQLite; at single-user scale the row set is small.
        var windows = await context.Rides
            .Where(r => r.UserId == userId)
            .Select(r => new { r.Id, r.StartTime, r.EndTime })
            .ToListAsync(cancellationToken);
        var match = windows.FirstOrDefault(w => RideOverlap.Intersects(w.StartTime, w.EndTime, parsed.StartTime, parsed.EndTime));
        if (match is not null)
        {
            // A Bryton FIT enriches the matched Polar ride with its temperature series; a GPX/TCX
            // covering the same window is just a duplicate of an existing ride, so it is skipped.
            return format == RawFileFormat.Fit
                ? await EnrichWithFitAsync(match.Id, parsed, file, userId, cancellationToken)
                : new FileImportResult(file.FileName, ImportOutcome.Skipped);
        }

        var ride = new Ride
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            StartTime = parsed.StartTime,
            EndTime = parsed.EndTime,
            Duration = parsed.Duration,
            DistanceMeters = parsed.DistanceMeters,
            AverageSpeedKmh = parsed.AverageSpeedKmh,
            MaximumSpeedKmh = parsed.MaximumSpeedKmh,
            AverageHeartRate = parsed.AverageHeartRate,
            MaximumHeartRate = parsed.MaximumHeartRate,
            ElevationGainMeters = parsed.ElevationGainMeters,
            AverageCadence = parsed.AverageCadence,
            Calories = parsed.Calories,
            AverageTemperatureCelsius = parsed.AverageTemperatureCelsius,
            MinTemperatureCelsius = parsed.MinTemperatureCelsius,
            MaxTemperatureCelsius = parsed.MaxTemperatureCelsius,
            Sport = parsed.Sport,
            Source = RideSource.Import,
            RoutePolyline = PolylineEncoder.Encode(Downsample(parsed.RoutePoints)),
            MetricSeries = MetricSeriesBuilder.BuildStorable(parsed.RoutePoints),
        };

        ride.RawFiles.Add(new RawFile
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Format = FormatFor(file.FileName),
            FileName = file.FileName,
            Content = file.Content,
            UploadedAt = DateTimeOffset.UtcNow,
        });

        context.Rides.Add(ride);
        await context.SaveChangesAsync(cancellationToken);

        return new FileImportResult(file.FileName, ImportOutcome.Imported, RideId: ride.Id);
    }

    private async Task<FileImportResult> EnrichWithFitAsync(
        Guid rideId, ParsedActivity parsed, ActivityFile file, string userId, CancellationToken cancellationToken)
    {
        var ride = await context.Rides
            .Include(r => r.RawFiles)
            .SingleAsync(r => r.Id == rideId, cancellationToken);

        // Idempotent: a ride that already carries a FIT has been enriched — don't duplicate it.
        if (ride.RawFiles.Any(f => f.Format == RawFileFormat.Fit))
        {
            return new FileImportResult(file.FileName, ImportOutcome.Skipped);
        }

        // Temperature-only enrichment: never overwrite the ride's own metrics, source or route.
        ride.AverageTemperatureCelsius = parsed.AverageTemperatureCelsius;
        ride.MinTemperatureCelsius = parsed.MinTemperatureCelsius;
        ride.MaxTemperatureCelsius = parsed.MaxTemperatureCelsius;

        context.RawFiles.Add(new RawFile
        {
            Id = Guid.NewGuid(),
            RideId = ride.Id,
            UserId = userId,
            Format = RawFileFormat.Fit,
            FileName = file.FileName,
            Content = file.Content,
            UploadedAt = DateTimeOffset.UtcNow,
        });

        await context.SaveChangesAsync(cancellationToken);
        return new FileImportResult(file.FileName, ImportOutcome.Imported, RideId: ride.Id);
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

        // Always keep the final point so the route ends where the ride did.
        if (sampled[^1].Equals(points[^1]) == false)
        {
            sampled.Add(points[^1]);
        }

        return sampled;
    }

    private static RawFileFormat FormatFor(string fileName) =>
        fileName.EndsWith(".tcx", StringComparison.OrdinalIgnoreCase) ? RawFileFormat.Tcx
        : fileName.EndsWith(".fit", StringComparison.OrdinalIgnoreCase) ? RawFileFormat.Fit
        : RawFileFormat.Gpx;
}
