using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RideLog.Application.Analysis;
using RideLog.Infrastructure.Analysis;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Analysis;

/// <summary>
/// The adapter over the provider. What is worth pinning here is not that Anthropic answers — that is
/// their business, and a test that called them would cost money to run — but the two things this app
/// decides: which model writes, and that an app with no key can still boot and answer (#187).
/// </summary>
public class TrainingAnalystTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private static AnthropicTrainingAnalyst Analyst(string? model = null, string? apiKey = "a-key") =>
        new(Options.Create(new AiOptions { ApiKey = apiKey ?? string.Empty, Model = model ?? new AiOptions().Model }));

    /// <summary>
    /// The default is written down in one place and is the model the decision in #187 named. Anything
    /// else is a setting the owner changed on purpose, which is the point: swapping model is one line
    /// of configuration, not a deploy — the same shape as Mail:FromAddress.
    /// </summary>
    [Fact]
    public void The_default_model_is_the_one_the_decision_named()
    {
        Assert.Equal("claude-opus-5", Analyst().Model);
    }

    [Fact]
    public void Configuration_can_name_a_different_model()
    {
        Assert.Equal("claude-sonnet-5", Analyst(model: "claude-sonnet-5").Model);
    }

    /// <summary>
    /// An app with no key configured must still resolve this: <c>/statistics</c> is public and asks
    /// whether the section exists, so a constructor that demanded a key would take the whole page
    /// down for want of a feature nobody switched on.
    /// </summary>
    [Fact]
    public void An_app_with_no_key_still_resolves_an_analyst()
    {
        using var scope = factory.Services.CreateScope();

        var analyst = scope.ServiceProvider.GetRequiredService<ITrainingAnalyst>();

        Assert.False(string.IsNullOrWhiteSpace(analyst.Model));
    }

    /// <summary>
    /// And asking it to write with no key is a clear failure rather than whatever the provider says
    /// to an empty bearer token — the lesson from the Resend 403, where "it failed" hid that the
    /// mistake was a setting.
    /// </summary>
    [Fact]
    public async Task Asking_with_no_key_says_so_plainly()
    {
        var summary = new MonthlyTrainingSummary(
            new RideLog.Application.Rides.MonthlyAggregate(2026, 6, 0, 0, 0, 0, 0), [], [], null, null);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Analyst(apiKey: string.Empty).AnalyseAsync(summary, AnalysisLanguage.Hungarian));

        Assert.Contains("Ai:ApiKey", error.Message, StringComparison.Ordinal);
    }
}
