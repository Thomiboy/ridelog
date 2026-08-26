namespace RideLog.Application.Analysis;

/// <summary>
/// Writes a month up (#187, docs/adr/0008).
///
/// It takes a <see cref="MonthlyTrainingSummary"/> and a language, and nothing else. No rider, no
/// database, no way to ask a question of its own: what it was handed is what it can see, and what it
/// can see is what leaves the app. That is the same guarantee by signature as
/// <c>IOwnerMailSender.NotifyOwnerAsync</c> taking no recipient (#168) — a second caller wanting more
/// is a new method and a new decision, not a quiet widening of this one.
/// </summary>
public interface ITrainingAnalyst
{
    /// <summary>
    /// The analysis, as prose in the given language. Throws when the provider refuses or fails —
    /// nothing is stored in that case, so a failed call costs only the call.
    /// </summary>
    Task<string> AnalyseAsync(
        MonthlyTrainingSummary summary, AnalysisLanguage language, CancellationToken cancellationToken = default);

    /// <summary>Which model this writes with, recorded alongside what it wrote.</summary>
    string Model { get; }
}
