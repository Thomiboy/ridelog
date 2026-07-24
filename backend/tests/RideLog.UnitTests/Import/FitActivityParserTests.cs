using Dynastream.Fit;
using RideLog.Infrastructure.Import;
using File = Dynastream.Fit.File;

namespace RideLog.UnitTests.Import;

public sealed class FitActivityParserTests
{
    /// <summary>
    /// Builds a minimal but valid FIT byte payload with the SDK encoder: a FileId, the given
    /// per-record samples (timestamp, position, altitude, temperature), and a Session summary.
    /// The expected values in the tests come from these inputs, decoded back independently.
    /// </summary>
    private static byte[] BuildFit(
        (System.DateTime Time, double Lat, double Lon, float Altitude, sbyte Temperature, byte HeartRate)[] records,
        Sport sport = Sport.Cycling,
        float totalDistanceMeters = 25000f,
        float totalTimerSeconds = 3600f)
    {
        using var stream = new MemoryStream();
        var encoder = new Encode(ProtocolVersion.V20);
        encoder.Open(stream);

        var fileId = new FileIdMesg();
        fileId.SetType(File.Activity);
        fileId.SetTimeCreated(new Dynastream.Fit.DateTime(records[0].Time));
        encoder.Write(fileId);

        foreach (var r in records)
        {
            var record = new RecordMesg();
            record.SetTimestamp(new Dynastream.Fit.DateTime(r.Time));
            record.SetPositionLat(Semicircles(r.Lat));
            record.SetPositionLong(Semicircles(r.Lon));
            record.SetAltitude(r.Altitude);
            record.SetTemperature(r.Temperature);
            record.SetHeartRate(r.HeartRate);
            encoder.Write(record);
        }

        var session = new SessionMesg();
        session.SetStartTime(new Dynastream.Fit.DateTime(records[0].Time));
        session.SetSport(sport);
        session.SetTotalDistance(totalDistanceMeters);
        session.SetTotalTimerTime(totalTimerSeconds);
        encoder.Write(session);

        encoder.Close();
        return stream.ToArray();
    }

    private static int Semicircles(double degrees) => (int)(degrees / 180.0 * int.MaxValue);

    private static readonly System.DateTime T0 = new(2026, 6, 1, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Parses_temperature_series_timing_and_route_from_a_fit_file()
    {
        var bytes = BuildFit(
        [
            (T0, 47.50, 19.00, 100f, (sbyte)10, (byte)120),
            (T0.AddMinutes(30), 47.55, 19.05, 150f, (sbyte)20, (byte)140),
            (T0.AddHours(1), 47.60, 19.10, 120f, (sbyte)15, (byte)130),
        ]);

        var parser = new FitActivityParser();

        Assert.True(parser.CanParse("ride.fit"));
        Assert.False(parser.CanParse("ride.gpx"));

        using var content = new MemoryStream(bytes);
        var parsed = parser.Parse(content, "ride.fit");

        Assert.Equal(new DateTimeOffset(T0, TimeSpan.Zero), parsed.StartTime);
        Assert.Equal(new DateTimeOffset(T0.AddHours(1), TimeSpan.Zero), parsed.EndTime);

        // Temperature series 10/20/15 → avg 15, min 10, max 20.
        Assert.Equal(15, parsed.AverageTemperatureCelsius!.Value, 0.01);
        Assert.Equal(10, parsed.MinTemperatureCelsius!.Value, 0.01);
        Assert.Equal(20, parsed.MaxTemperatureCelsius!.Value, 0.01);

        Assert.Equal(3, parsed.RoutePoints.Count);
        Assert.Equal(47.50, parsed.RoutePoints[0].Latitude, 0.001);
        Assert.Equal("Cycling", parsed.Sport);
    }

    [Fact]
    public void Keeps_per_point_heart_rate_on_the_route()
    {
        var bytes = BuildFit(
        [
            (T0, 47.50, 19.00, 100f, (sbyte)10, (byte)120),
            (T0.AddMinutes(30), 47.55, 19.05, 150f, (sbyte)20, (byte)145),
        ]);

        var parsed = new FitActivityParser().Parse(new MemoryStream(bytes), "ride.fit");

        Assert.Equal([120, 145], parsed.RoutePoints.Select(p => p.HeartRate));
    }
}
