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
        float totalTimerSeconds = 3600f,
        float? speedMps = null,
        float[]? recordSpeedsMps = null,
        float? sessionMaxSpeedMps = null)
    {
        using var stream = new MemoryStream();
        var encoder = new Encode(ProtocolVersion.V20);
        encoder.Open(stream);

        var fileId = new FileIdMesg();
        fileId.SetType(File.Activity);
        fileId.SetTimeCreated(new Dynastream.Fit.DateTime(records[0].Time));
        encoder.Write(fileId);

        for (var i = 0; i < records.Length; i++)
        {
            var r = records[i];
            var record = new RecordMesg();
            record.SetTimestamp(new Dynastream.Fit.DateTime(r.Time));
            record.SetPositionLat(Semicircles(r.Lat));
            record.SetPositionLong(Semicircles(r.Lon));
            record.SetAltitude(r.Altitude);
            record.SetTemperature(r.Temperature);
            record.SetHeartRate(r.HeartRate);
            if (recordSpeedsMps is { } perRecord)
            {
                record.SetSpeed(perRecord[i]);
            }
            else if (speedMps is { } speed)
            {
                record.SetSpeed(speed);
            }

            encoder.Write(record);
        }

        var session = new SessionMesg();
        session.SetStartTime(new Dynastream.Fit.DateTime(records[0].Time));
        session.SetSport(sport);
        session.SetTotalDistance(totalDistanceMeters);
        session.SetTotalTimerTime(totalTimerSeconds);
        if (sessionMaxSpeedMps is { } sessionMax)
        {
            session.SetMaxSpeed(sessionMax);
        }
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
    public void Keeps_the_per_record_speed_the_device_recorded()
    {
        // FIT records speed in metres per second: 7.5 m/s = 27 km/h.
        var bytes = BuildFit(
            [
                (T0, 47.50, 19.00, 100f, (sbyte)10, (byte)120),
                (T0.AddMinutes(30), 47.55, 19.05, 150f, (sbyte)20, (byte)140),
            ],
            speedMps: 7.5f);

        using var content = new MemoryStream(bytes);
        var parsed = new FitActivityParser().Parse(content, "ride.fit");

        Assert.Equal(27.0, parsed.RoutePoints[0].SpeedKmh!.Value, 0.01);
    }

    [Fact]
    public void Duration_is_the_session_timer_not_the_elapsed_span()
    {
        // Records span a full hour, but the session's timer says 45 minutes of moving time.
        var bytes = BuildFit(
            [
                (T0, 47.50, 19.00, 100f, (sbyte)10, (byte)120),
                (T0.AddHours(1), 47.55, 19.05, 150f, (sbyte)20, (byte)140),
            ],
            totalTimerSeconds: 2700f);

        using var content = new MemoryStream(bytes);

        Assert.Equal(TimeSpan.FromMinutes(45), new FitActivityParser().Parse(content, "ride.fit").Duration);
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

    [Fact]
    public void Keeps_per_point_temperature_on_the_route()
    {
        var bytes = BuildFit(
        [
            (T0, 47.50, 19.00, 100f, (sbyte)8, (byte)120),
            (T0.AddMinutes(30), 47.55, 19.05, 150f, (sbyte)17, (byte)145),
        ]);

        var parsed = new FitActivityParser().Parse(new MemoryStream(bytes), "ride.fit");

        Assert.Equal([8.0, 17.0], parsed.RoutePoints.Select(p => p.TemperatureCelsius));
    }

    [Fact]
    public void Summarises_temperature_from_the_same_records_the_route_is_built_from()
    {
        // A device switched on before it has a GPS fix logs records with no position, and the
        // temperature sensor reads its power-on default until it settles. Those records are dropped
        // from the route, so the graph never shows the bogus reading — but they must not reach the
        // ride's min/avg/max either, or a July ride reports a 0 °C low that appears nowhere on it.
        var start = new System.DateTime(2026, 7, 28, 6, 0, 0, DateTimeKind.Utc);
        var samples = Enumerable.Range(0, 60)
            .Select(i => (Time: start.AddSeconds(i), Temp: (sbyte)(i < 5 ? 0 : 26)))
            .ToArray();
        var bytes = TestFit.Build(samples, unpositionedPrefix: 5);

        var parsed = new FitActivityParser().Parse(new MemoryStream(bytes), "ride.fit");

        // The route only ever saw 26 °C, so the summary must agree with it.
        Assert.All(parsed.RoutePoints, p => Assert.Equal(26, p.TemperatureCelsius));
        Assert.Equal(26, parsed.MinTemperatureCelsius);
        Assert.Equal(26, parsed.AverageTemperatureCelsius);
    }

    [Fact]
    public void Exposes_the_session_maximum_as_the_device_summary()
    {
        // 23.6 m/s × 3.6 ≈ 85 km/h — a glitch, reported verbatim. The parser doesn't judge it; the
        // ride's top speed is derived from the route by whoever stores it (docs/adr/0002).
        var records = Enumerable.Range(0, 5)
            .Select(i => (T0.AddSeconds(i), 47.50 + i * 0.0001, 19.00, 100f, (sbyte)20, (byte)140))
            .ToArray();
        var bytes = BuildFit(records, sessionMaxSpeedMps: 23.6f);

        var parsed = new FitActivityParser().Parse(new MemoryStream(bytes), "ride.fit");

        Assert.Equal(84.96, parsed.DeviceMaximumSpeedKmh!.Value, 0.01);
    }
}
