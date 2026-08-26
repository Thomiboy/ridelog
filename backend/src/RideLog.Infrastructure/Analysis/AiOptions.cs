namespace RideLog.Infrastructure.Analysis;

/// <summary>
/// What the monthly analysis needs to reach a model (#187). In App Service these arrive as
/// <c>Ai__ApiKey</c> and friends, the same way the mail settings do.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Unset means the feature is unavailable, whatever the owner's switch says.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The model that writes. Opus 5 by default: across a whole log the difference against a cheaper
    /// tier is a couple of dollars once, so this was never a cost decision — the only question is
    /// whether the text is worth reading (#187). A setting, so changing that answer is one line
    /// rather than a deploy, the same shape as <c>Mail:FromAddress</c>.
    /// </summary>
    public string Model { get; set; } = "claude-opus-5";

    /// <summary>
    /// How long one request may take. Explicit because a stuck call would otherwise hold the request
    /// open for minutes — and on App Service F1 that is a request nobody gets back.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 60;

    /// <summary>Room for the prose; an analysis is a few hundred tokens, not a book.</summary>
    public int MaxTokens { get; set; } = 4096;
}
