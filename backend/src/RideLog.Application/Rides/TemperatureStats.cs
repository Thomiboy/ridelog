namespace RideLog.Application.Rides;

/// <summary>
/// Distance ridden in one 5°C temperature band. Open-ended bands use a null bound
/// (below 0°C = From null, To 0; 25°C and above = From 25, To null).
/// </summary>
public sealed record TemperatureBandSlice(int? FromCelsius, int? ToCelsius, double Km);

/// <summary>The coldest or warmest ride, by its average temperature; links back to that ride.</summary>
public sealed record TemperatureExtreme(Guid Id, DateTimeOffset Date, double AverageTemperatureCelsius);

/// <summary>Average ridden temperature in one calendar month.</summary>
public sealed record MonthlyTemperature(int Year, int Month, double AverageTemperatureCelsius);

/// <summary>The Statistics page's Temperature section; null when no ride carries temperature.</summary>
public sealed record TemperatureStats(
    IReadOnlyList<TemperatureBandSlice> Distribution,
    TemperatureExtreme? Coldest,
    TemperatureExtreme? Warmest,
    double? SeasonMinCelsius,
    double? SeasonMaxCelsius,
    IReadOnlyList<MonthlyTemperature> MonthlyAverage);
