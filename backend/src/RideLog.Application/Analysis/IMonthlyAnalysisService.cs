namespace RideLog.Application.Analysis;

/// <summary>One stored analysis, as a reader of the Statistics page needs it.</summary>
public sealed record MonthlyAnalysisDto(
    Guid Id,
    int Year,
    int Month,
    AnalysisLanguage Language,
    string Text,
    string Model,
    int RideCount,
    DateTimeOffset WrittenAt);

/// <summary>
/// Why a request to write one was turned down. Refusals are ordinary answers here, not errors: the
/// whole quota is structural, so "there is already one of these" is the system working (#187).
/// </summary>
public enum AnalysisRefusal
{
    /// <summary>Nothing was refused.</summary>
    None,

    /// <summary>The feature is switched off, or no API key is configured.</summary>
    Unavailable,

    /// <summary>A closed month that already has an analysis in this language. Delete it to write again.</summary>
    AlreadyWritten,

    /// <summary>The running month, unchanged since it was last written. Ride, then ask again.</summary>
    Unchanged,
}

/// <summary>What came of asking for one: the analysis, or the reason there isn't a new one.</summary>
public sealed record MonthlyAnalysisOutcome(MonthlyAnalysisDto? Analysis, AnalysisRefusal Refusal)
{
    public static MonthlyAnalysisOutcome Written(MonthlyAnalysisDto analysis) => new(analysis, AnalysisRefusal.None);

    public static MonthlyAnalysisOutcome Refused(AnalysisRefusal refusal) => new(null, refusal);
}

/// <summary>
/// The monthly analysis, end to end: decide whether one may be written, write it, keep it, hand it
/// back, and let it go (#187).
///
/// Every method names the rider it is for, like every other read here (docs/adr/0006). Unlike the
/// contact form, this stores *after* the call rather than before it — there is nothing to lose ahead
/// of the call, because the call's result is the whole value.
/// </summary>
public interface IMonthlyAnalysisService
{
    /// <summary>Whether the section exists at all: the switch is on and a key is configured.</summary>
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);

    /// <summary>The kill switch. Stored, so flipping it takes effect without a restart (#172).</summary>
    Task SetAvailableAsync(bool available, CancellationToken cancellationToken = default);

    /// <summary>The stored analysis for this rider's month in this language, or null.</summary>
    Task<MonthlyAnalysisDto?> ReadAsync(
        string riderId, int year, int month, AnalysisLanguage language, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes one, if this month may be written. Generation only ever happens here, and only when
    /// somebody asked: nothing in this app writes an analysis as a side effect of anything else.
    /// </summary>
    Task<MonthlyAnalysisOutcome> WriteAsync(
        string riderId, int year, int month, AnalysisLanguage language, CancellationToken cancellationToken = default);

    /// <summary>Removes one of this rider's analyses; false when they have no such analysis.</summary>
    Task<bool> DeleteAsync(string riderId, Guid id, CancellationToken cancellationToken = default);
}
