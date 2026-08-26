namespace RideLog.Application.Analysis;

/// <summary>
/// Which language an analysis was written in. Generated prose cannot pass through Transloco — it is
/// regenerated or it is not — so the language is part of what identifies a stored analysis, not a
/// display choice made later (#187).
///
/// Travels as a name on the wire, never as an ordinal: an ordinal would tie the format to the order
/// these are declared in.
/// </summary>
public enum AnalysisLanguage
{
    Hungarian,
    English,
}
