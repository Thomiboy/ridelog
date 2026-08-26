using System.Globalization;
using RideLog.Application.Analysis;
using RideLog.Application.Rides;
using RideLog.Infrastructure.Analysis;

namespace RideLog.UnitTests.Analysis;

/// <summary>
/// What we actually send. The figures are rendered by a pure function so the one thing that can be
/// checked about a request to a language model — that it was handed the right numbers, and only
/// those — is checkable without spending a token (#187).
/// </summary>
public class MonthlyAnalysisPromptTests
{
    private static MonthlyTrainingSummary Summary() => new(
        new MonthlyAggregate(2026, 7, 100.0, 600, 2, 1300, 240.0),
        [
            new MonthlyAggregate(2026, 3, 100.0, 500, 1, 1500, 120.0),
            new MonthlyAggregate(2026, 6, 42.5, 210, 1, 600, 95.0),
        ],
        [
            new AnalysedRide(new DateOnly(2026, 7, 5), 60.0, 120.0, 142, 171, 400, 24.5, 12.0, 0.6),
            new AnalysedRide(new DateOnly(2026, 7, 12), 40.0, 120.0, null, null, 200, null, null, null),
        ],
        [new HrZoneSlice(1, 30.0), new HrZoneSlice(2, 0.0), new HrZoneSlice(3, 30.0),
         new HrZoneSlice(4, 60.0), new HrZoneSlice(5, 0.0)],
        [new TemperatureBandSlice(15, 20, 10.0), new TemperatureBandSlice(20, 25, 10.0),
         new TemperatureBandSlice(25, null, 20.0)]);

    private const string Expected = """
        Month: 2026-07
        Totals: 100 km, 240 min moving, 600 m climbed, 2 rides, 1300 kcal

        Preceding months (oldest first, km / min / m climbed / rides / kcal):
        2026-03: 100 / 120 / 500 / 1 / 1500
        2026-06: 42.5 / 95 / 210 / 1 / 600

        Rides:
        2026-07-05: 60 km, 120 min, 400 m climbed, HR avg 142, HR max 171, 24.5 C, wind 12 km/h, rain 0.6 mm
        2026-07-12: 40 km, 120 min, 200 m climbed

        Minutes in heart-rate zones (Z1 is easiest, Z5 hardest; zone floors are 50/60/70/80/90% of the rider's configured maximum heart rate):
        Z1: 30
        Z2: 0
        Z3: 30
        Z4: 60
        Z5: 0

        Kilometres ridden per temperature band:
        15 to 20 C: 10
        20 to 25 C: 10
        25 C and above: 20
        """;

    [Fact]
    public void The_figures_render_exactly_as_pinned()
    {
        Assert.Equal(Expected, MonthlyAnalysisPrompt.Figures(Summary()));
    }

    /// <summary>
    /// Rendered under a comma-decimal culture the numbers must not change: the sample above is what
    /// a reviewer checked, and a build machine's locale is no business of the request we send. The
    /// same class of bug as an Open-Meteo timestamp read as local time — plausible, and wrong.
    /// </summary>
    [Fact]
    public void The_running_machines_locale_does_not_change_what_is_sent()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("hu-HU");
            Assert.Equal(Expected, MonthlyAnalysisPrompt.Figures(Summary()));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// A month with nothing in it still renders — an analysis that says "you did not ride" is a fair
    /// answer, and it must not arrive as an exception.
    /// </summary>
    [Fact]
    public void An_empty_month_still_renders()
    {
        var empty = new MonthlyTrainingSummary(new MonthlyAggregate(2026, 2, 0, 0, 0, 0, 0), [], [], null, null);

        var figures = MonthlyAnalysisPrompt.Figures(empty);

        Assert.Contains("Month: 2026-02", figures);
        Assert.Contains("No rides this month.", figures);
    }

    /// <summary>
    /// The four limits are the feature's edges, not its tone (#187): only the supplied figures, no
    /// medical cause, say when a month is too thin, and name what a zone claim rests on. They are
    /// asserted as being present rather than word for word — the wording will be tuned, the limits
    /// will not.
    /// </summary>
    [Theory]
    [InlineData(AnalysisLanguage.Hungarian, "magyar")]
    [InlineData(AnalysisLanguage.English, "English")]
    public void The_instructions_carry_the_four_limits_and_name_the_language(AnalysisLanguage language, string named)
    {
        var instructions = MonthlyAnalysisPrompt.Instructions(language);

        Assert.Contains(named, instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("only the figures", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("medical", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("too few rides", instructions, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("maximum heart rate", instructions, StringComparison.OrdinalIgnoreCase);
    }
}
