using System.Globalization;
using System.Text;
using RideLog.Application.Analysis;
using RideLog.Application.Rides;

namespace RideLog.Infrastructure.Analysis;

/// <summary>
/// Turns a month into the two halves of one request: the instructions that bound what may be said,
/// and the figures it may be said about (#187).
///
/// Pure on purpose. The only thing that can be checked about a request to a language model without
/// spending a token is that it was handed the right numbers and no others — so that part is a
/// function, pinned by a test, rather than string-building buried in the client.
/// </summary>
internal static class MonthlyAnalysisPrompt
{
    /// <summary>
    /// The four limits (#187) plus the language. They are the feature's edges, not its manners: the
    /// model may only read the figures it was given, may not reach for a medical cause, must say so
    /// when a month is too thin to generalise from, and must say what a zone claim rests on — the
    /// rider's own configured maximum, which is a number they typed and may well have typed wrong.
    /// </summary>
    public static string Instructions(AnalysisLanguage language)
    {
        var written = language == AnalysisLanguage.Hungarian ? "Hungarian (magyar)" : "English";

        return $"""
            You are reading one month of a road cyclist's own training log and writing a short, plain
            account of it for the rider themselves. Write in {written}. Aim for four or five short
            paragraphs: what the month was made of, how it sits against the months before it, and one
            or two things worth trying next.

            Hold to these limits:

            - Use only the figures given below. Do not compute new ones, do not estimate, and do not
              carry in anything you know about cycling training loads as if it were this rider's data.
              Every number you write must appear above your eyes.
            - Name what the data shows; never infer a medical cause from it. If something looks
              unusual, say what is unusual about the numbers and stop there.
            - Say plainly when there are too few rides to generalise from, rather than generalising
              anyway. A thin month is a fact about the month, not a gap to fill.
            - Any claim about heart-rate zones rests on the rider's configured maximum heart rate,
              which they set themselves. Say so where you lean on it.

            Suggestions are welcome and are the point of the exercise, but they are suggestions about
            what the numbers invite, not a training plan: there is no power data, no rest or recovery
            data, and no injury history here, so anything shaped like a prescription would be invented.
            """;
    }

    /// <summary>
    /// The month as text. Rendered under the invariant culture, because a build or hosting machine's
    /// locale has no business changing what gets sent — the same class of mistake as reading an
    /// Open-Meteo timestamp as local time: plausible-looking, and wrong.
    /// </summary>
    public static string Figures(MonthlyTrainingSummary summary)
    {
        var text = new StringBuilder();
        var month = summary.Month;

        text.Append(Invariant($"Month: {month.Year:D4}-{month.Month:D2}"));
        text.Append(Invariant($"\nTotals: {N(month.DistanceKm)} km, {N(month.DurationMinutes)} min moving, "));
        text.Append(Invariant($"{N(month.ElevationGainMeters)} m climbed, {month.RideCount} rides, {month.Calories} kcal"));

        if (summary.PrecedingMonths.Count > 0)
        {
            text.Append("\n\nPreceding months (oldest first, km / min / m climbed / rides / kcal):");
            foreach (var past in summary.PrecedingMonths)
            {
                text.Append(Invariant($"\n{past.Year:D4}-{past.Month:D2}: {N(past.DistanceKm)} / {N(past.DurationMinutes)}"));
                text.Append(Invariant($" / {N(past.ElevationGainMeters)} / {past.RideCount} / {past.Calories}"));
            }
        }

        text.Append("\n\nRides:");
        if (summary.Rides.Count == 0)
        {
            text.Append("\nNo rides this month.");
        }
        else
        {
            foreach (var ride in summary.Rides)
            {
                text.Append('\n').Append(Ride(ride));
            }
        }

        if (summary.HrZones is { Count: > 0 } zones)
        {
            text.Append("\n\nMinutes in heart-rate zones (Z1 is easiest, Z5 hardest; zone floors are ");
            text.Append("50/60/70/80/90% of the rider's configured maximum heart rate):");
            foreach (var zone in zones)
            {
                text.Append(Invariant($"\nZ{zone.Zone}: {N(zone.Minutes)}"));
            }
        }

        if (summary.TemperatureBands is { Count: > 0 } bands)
        {
            text.Append("\n\nKilometres ridden per temperature band:");
            foreach (var band in bands.Where(b => b.Km > 0))
            {
                text.Append(Invariant($"\n{Band(band)}: {N(band.Km)}"));
            }
        }

        return text.ToString();
    }

    /// <summary>One ride, naming only what it actually recorded — an absent reading is left out.</summary>
    private static string Ride(AnalysedRide ride)
    {
        var parts = new List<string>
        {
            Invariant($"{ride.Date:yyyy-MM-dd}: {N(ride.DistanceKm)} km"),
            Invariant($"{N(ride.DurationMinutes)} min"),
        };

        if (ride.ElevationGainMeters is { } climb)
        {
            parts.Add(Invariant($"{N(climb)} m climbed"));
        }

        if (ride.AverageHeartRate is { } average)
        {
            parts.Add(Invariant($"HR avg {average}"));
        }

        if (ride.MaximumHeartRate is { } maximum)
        {
            parts.Add(Invariant($"HR max {maximum}"));
        }

        if (ride.AverageTemperatureCelsius is { } temperature)
        {
            parts.Add(Invariant($"{N(temperature)} C"));
        }

        if (ride.WindSpeedKmh is { } wind)
        {
            parts.Add(Invariant($"wind {N(wind)} km/h"));
        }

        if (ride.PrecipitationMm is { } rain)
        {
            parts.Add(Invariant($"rain {N(rain)} mm"));
        }

        return string.Join(", ", parts);
    }

    private static string Band(TemperatureBandSlice band) => (band.FromCelsius, band.ToCelsius) switch
    {
        (null, { } to) => Invariant($"below {to} C"),
        ({ } from, null) => Invariant($"{from} C and above"),
        ({ } from, { } to) => Invariant($"{from} to {to} C"),
        _ => "unbounded",
    };

    /// <summary>A number with no trailing zeros: "100", not "100.0" — it is read, not parsed.</summary>
    private static string N(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Invariant(FormattableString text) => FormattableString.Invariant(text);
}
