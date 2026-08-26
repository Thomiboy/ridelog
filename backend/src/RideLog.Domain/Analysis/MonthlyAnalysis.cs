namespace RideLog.Domain.Analysis;

/// <summary>
/// One written reading of one rider's calendar month (#187).
///
/// Rider, year, month and language together identify it, and that quadruple is also the whole quota:
/// a closed month never changes, so its analysis is written once and read free from then on. There is
/// no counter anywhere — the key is the ceiling.
/// </summary>
public class MonthlyAnalysis
{
    public Guid Id { get; init; }

    public required string UserId { get; init; }

    public required int Year { get; init; }

    public required int Month { get; init; }

    /// <summary>Stored as its name, not its ordinal, for the same reason it travels as one.</summary>
    public required string Language { get; init; }

    public required string Text { get; init; }

    /// <summary>Which model wrote it, so a change of model is visible in what it produced.</summary>
    public required string Model { get; init; }

    /// <summary>
    /// How many rides the month held when this was written. The running month may be written again
    /// only once this has changed — so the ceiling on a live month is the number of rides in it, not
    /// the number of times somebody presses a button.
    /// </summary>
    public required int RideCount { get; init; }

    public DateTimeOffset WrittenAt { get; init; }
}
