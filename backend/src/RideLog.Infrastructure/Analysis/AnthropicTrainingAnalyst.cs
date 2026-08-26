using Anthropic;
using Anthropic.Models.Messages;
using Microsoft.Extensions.Options;
using RideLog.Application.Analysis;

namespace RideLog.Infrastructure.Analysis;

/// <summary>
/// Writes a month up through Anthropic's Messages API (#187, docs/adr/0008).
///
/// The official SDK rather than a hand-rolled <c>HttpClient</c>, and that is the one deliberate break
/// from how this codebase talks to third parties. The Polar, Open-Meteo and Resend clients are
/// hand-rolled because they aim at stationary targets — one POST, one GET, unchanged for years. The
/// Messages API is not stationary, and its movements are the kind that fail quietly: a refusal
/// arrives as a stop reason rather than an error status, and reading the content without checking it
/// yields an empty string that looks like a model with nothing to say.
///
/// The client is built per call, not per instance, so an app with no key configured still resolves
/// this and answers <c>/statistics</c> — that endpoint is public, and it must not go down for want of
/// a feature nobody switched on.
/// </summary>
internal sealed class AnthropicTrainingAnalyst(IOptions<AiOptions> options) : ITrainingAnalyst
{
    public string Model => options.Value.Model;

    public async Task<string> AnalyseAsync(
        MonthlyTrainingSummary summary, AnalysisLanguage language, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            // Said plainly rather than left to the provider's answer to an empty bearer token. The
            // lesson from the Resend 403: "it failed" hid that the mistake was a setting (#168).
            throw new InvalidOperationException(
                "No Ai:ApiKey is configured, so no monthly analysis can be written.");
        }

        var client = new AnthropicClient
        {
            ApiKey = config.ApiKey,
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
        };

        var response = await client.Messages.Create(
            new MessageCreateParams
            {
                Model = config.Model,
                MaxTokens = config.MaxTokens,
                System = MonthlyAnalysisPrompt.Instructions(language),
                Messages = [new() { Role = Role.User, Content = MonthlyAnalysisPrompt.Figures(summary) }],
            },
            cancellationToken: cancellationToken);

        // Checked before the content is read, never after: a refused response carries no text, and
        // reading it first would store an empty analysis as though the model simply had little to say.
        if (response.StopReason == "refusal")
        {
            throw new InvalidOperationException(
                $"The model declined to write this month up: {response.StopDetails?.Explanation ?? "no explanation given"}");
        }

        var text = string.Concat(response.Content.Select(block => block.Value).OfType<TextBlock>().Select(b => b.Text));

        return string.IsNullOrWhiteSpace(text)
            ? throw new InvalidOperationException(
                $"The model returned no text (stop reason: {response.StopReason ?? "none"}).")
            : text;
    }
}
