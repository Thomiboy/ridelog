using RideLog.Application.Rides;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Rides;

namespace RideLog.UnitTests.Rides;

public sealed class TemperatureBandCalculatorTests
{
    private static MetricSample At(double distanceKm, double? temperature) =>
        new(distanceKm, ElapsedMinutes: 0, ElevationMeters: null, HeartRate: null, temperature);

    [Fact]
    public void Attributes_each_segment_distance_to_the_band_of_its_starting_temperature()
    {
        var series = new List<MetricSample>
        {
            At(0, 3),  // 0–5 °C owns the 0→2 km segment
            At(2, 8),  // 5–10 °C owns the 2→5 km segment
            At(5, 22), // last sample, owns nothing forward
        };

        var bands = TemperatureBandCalculator.KmPerBand(series);

        // Seven fixed 5°C bands, zeros included.
        Assert.Equal(7, bands.Count);
        Assert.Equal(2, Km(bands, 0, 5), 0.01);
        Assert.Equal(3, Km(bands, 5, 10), 0.01);
        Assert.Equal(0, Km(bands, 10, 15), 0.01);
    }

    [Fact]
    public void Places_sub_zero_and_over_25_into_the_open_ended_bands()
    {
        var series = new List<MetricSample>
        {
            At(0, -3),  // below 0 owns 0→1
            At(1, 27),  // 25+ owns 1→4
            At(4, 27),
        };

        var bands = TemperatureBandCalculator.KmPerBand(series);

        Assert.Equal(1, bands.Single(b => b.ToCelsius == 0 && b.FromCelsius is null).Km, 0.01);
        Assert.Equal(3, bands.Single(b => b.FromCelsius == 25 && b.ToCelsius is null).Km, 0.01);
    }

    private static double Km(IReadOnlyList<TemperatureBandSlice> bands, int from, int to) =>
        bands.Single(b => b.FromCelsius == from && b.ToCelsius == to).Km;
}
