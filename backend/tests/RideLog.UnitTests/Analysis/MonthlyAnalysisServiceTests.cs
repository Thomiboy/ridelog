using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideLog.Application.Analysis;
using RideLog.Domain.Rides;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Analysis;

/// <summary>
/// Stands in for the model. It records what it was asked and how often, because the point of nearly
/// every rule below is that a *second* ask never reaches it — the quota is structural, and a quota
/// you can spend twice is not one.
/// </summary>
public sealed class RecordingAnalyst : ITrainingAnalyst
{
    public List<(MonthlyTrainingSummary Summary, AnalysisLanguage Language)> Asked { get; } = [];

    public string Model => "test-model";

    /// <summary>Set to make the provider fail — a refusal and an outage look the same from here.</summary>
    public bool Throws { get; set; }

    public Task<string> AnalyseAsync(
        MonthlyTrainingSummary summary, AnalysisLanguage language, CancellationToken cancellationToken = default)
    {
        Asked.Add((summary, language));
        return Throws
            ? throw new InvalidOperationException("the model declined")
            : Task.FromResult($"Reading of {summary.Month.Year}-{summary.Month.Month} in {language}.");
    }
}

/// <summary>
/// Boots the API with the model faked and a key configured. "Now" is July 2026, so July is the
/// running month and June is closed — the difference is a rule, not a detail.
/// </summary>
public class AnalysisApiFactory : RideLogApiFactory
{
    public static readonly DateTimeOffset Now = new(2026, 7, 17, 10, 0, 0, TimeSpan.Zero);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    public RecordingAnalyst Analyst { get; } = new();

    /// <summary>Set false to boot as an owner who never configured a key.</summary>
    protected virtual bool HasApiKey => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Ai:ApiKey", HasApiKey ? "test-ai-key" : string.Empty);
    }

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider());
        services.RemoveAll<ITrainingAnalyst>();
        services.AddSingleton<ITrainingAnalyst>(Analyst);
    }
}

/// <summary>The same app with no key configured at all.</summary>
public sealed class KeylessAnalysisApiFactory : AnalysisApiFactory
{
    protected override bool HasApiKey => false;
}

public class MonthlyAnalysisServiceTests(AnalysisApiFactory factory) : IClassFixture<AnalysisApiFactory>
{
    private const string Rider = "admin-1";
    private const string OtherRider = "rider-2";

    private static Ride Ride(DateTimeOffset start, string userId = Rider) => new()
    {
        Id = Guid.NewGuid(),
        UserId = userId,
        StartTime = start,
        EndTime = start.AddHours(2),
        Duration = TimeSpan.FromHours(2),
        DistanceMeters = 40_000,
        ElevationGainMeters = 200,
        Calories = 500,
        Sport = "ROAD_BIKING",
        Source = RideSource.Polar,
    };

    /// <summary>June 2026 (closed) and July 2026 (running) each hold one ride.</summary>
    private async Task ResetAsync(bool available = true)
    {
        factory.Analyst.Asked.Clear();
        factory.Analyst.Throws = false;

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
        context.MonthlyAnalyses.RemoveRange(context.MonthlyAnalyses);
        context.Rides.RemoveRange(context.Rides);
        await context.SaveChangesAsync();
        context.Rides.AddRange(
            Ride(new DateTimeOffset(2026, 6, 10, 8, 0, 0, TimeSpan.Zero)),
            Ride(new DateTimeOffset(2026, 7, 3, 8, 0, 0, TimeSpan.Zero)));
        await context.SaveChangesAsync();

        await scope.ServiceProvider.GetRequiredService<IMonthlyAnalysisService>().SetAvailableAsync(available);
    }

    private async Task<T> WithServiceAsync<T>(Func<IMonthlyAnalysisService, Task<T>> use)
    {
        using var scope = factory.Services.CreateScope();
        return await use(scope.ServiceProvider.GetRequiredService<IMonthlyAnalysisService>());
    }

    private Task<MonthlyAnalysisOutcome> WriteAsync(
        int year, int month, AnalysisLanguage language = AnalysisLanguage.Hungarian, string rider = Rider) =>
        WithServiceAsync(service => service.WriteAsync(rider, year, month, language));

    /// <summary>
    /// Off is the default the owner never has to think about, and it is deliberately the opposite of
    /// the contact form's: that costs nothing, this spends money. A feature that starts spending
    /// because nobody turned it off is the wrong default (#187).
    /// </summary>
    [Fact]
    public async Task With_the_switch_off_nothing_is_written_and_nothing_is_asked()
    {
        await ResetAsync(available: false);

        var outcome = await WriteAsync(2026, 6);

        Assert.Equal(AnalysisRefusal.Unavailable, outcome.Refusal);
        Assert.Null(outcome.Analysis);
        Assert.Empty(factory.Analyst.Asked);
    }

    [Fact]
    public async Task A_month_is_written_once_and_kept()
    {
        await ResetAsync();

        var outcome = await WriteAsync(2026, 6);

        Assert.Equal(AnalysisRefusal.None, outcome.Refusal);
        Assert.Equal("Reading of 2026-6 in Hungarian.", outcome.Analysis!.Text);
        Assert.Equal("test-model", outcome.Analysis.Model);
        Assert.Equal(1, outcome.Analysis.RideCount);
        Assert.Single(factory.Analyst.Asked);

        var stored = await WithServiceAsync(s => s.ReadAsync(Rider, 2026, 6, AnalysisLanguage.Hungarian));
        Assert.Equal(outcome.Analysis.Id, stored!.Id);
    }

    /// <summary>
    /// The whole quota, in one assertion: a closed month never changes, so its analysis is written
    /// once and read free from then on. No counter, no reset — the key is the ceiling.
    /// </summary>
    [Fact]
    public async Task A_closed_month_that_already_has_one_is_refused_without_asking_again()
    {
        await ResetAsync();
        await WriteAsync(2026, 6);

        var second = await WriteAsync(2026, 6);

        Assert.Equal(AnalysisRefusal.AlreadyWritten, second.Refusal);
        Assert.Single(factory.Analyst.Asked);
    }

    /// <summary>Two languages are two analyses: prose cannot be translated, only written again.</summary>
    [Fact]
    public async Task The_other_language_is_a_different_analysis()
    {
        await ResetAsync();
        await WriteAsync(2026, 6, AnalysisLanguage.Hungarian);

        var english = await WriteAsync(2026, 6, AnalysisLanguage.English);

        Assert.Equal(AnalysisRefusal.None, english.Refusal);
        Assert.Equal(2, factory.Analyst.Asked.Count);
        Assert.Equal(
            [AnalysisLanguage.Hungarian, AnalysisLanguage.English],
            factory.Analyst.Asked.Select(a => a.Language));
    }

    /// <summary>
    /// The running month is the one thing that could repeat, so the gate is the month itself: it may
    /// be written again once a ride has been added to it. The ceiling is the number of rides, not the
    /// number of times somebody presses a button.
    /// </summary>
    [Fact]
    public async Task The_running_month_is_refused_until_a_ride_is_added_to_it()
    {
        await ResetAsync();
        await WriteAsync(2026, 7);

        var unchanged = await WriteAsync(2026, 7);
        Assert.Equal(AnalysisRefusal.Unchanged, unchanged.Refusal);
        Assert.Single(factory.Analyst.Asked);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<RideLogDbContext>();
            context.Rides.Add(Ride(new DateTimeOffset(2026, 7, 15, 8, 0, 0, TimeSpan.Zero)));
            await context.SaveChangesAsync();
        }

        var afterRiding = await WriteAsync(2026, 7);
        Assert.Equal(AnalysisRefusal.None, afterRiding.Refusal);
        Assert.Equal(2, afterRiding.Analysis!.RideCount);
        Assert.Equal(2, factory.Analyst.Asked.Count);
    }

    /// <summary>
    /// A bad analysis is let go of and written again — a deliberate, visible act rather than a
    /// refresh button, which is what keeps the ceiling meaningful.
    /// </summary>
    [Fact]
    public async Task Deleting_one_lets_the_month_be_written_again()
    {
        await ResetAsync();
        var first = await WriteAsync(2026, 6);

        Assert.True(await WithServiceAsync(s => s.DeleteAsync(Rider, first.Analysis!.Id)));

        var second = await WriteAsync(2026, 6);
        Assert.Equal(AnalysisRefusal.None, second.Refusal);
        Assert.Equal(2, factory.Analyst.Asked.Count);
    }

    /// <summary>
    /// Storing after the call means a failed call leaves nothing — no half-written row, and no spent
    /// quota either, so the rider may simply try again. That is the accepted hole in a structural
    /// ceiling (#187): a genuine failure is worth retrying, and the kill switch is the backstop.
    /// </summary>
    [Fact]
    public async Task A_failed_call_stores_nothing_and_leaves_the_month_open()
    {
        await ResetAsync();
        factory.Analyst.Throws = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => WriteAsync(2026, 6));

        Assert.Null(await WithServiceAsync(s => s.ReadAsync(Rider, 2026, 6, AnalysisLanguage.Hungarian)));

        factory.Analyst.Throws = false;
        var retried = await WriteAsync(2026, 6);
        Assert.Equal(AnalysisRefusal.None, retried.Refusal);
    }

    /// <summary>
    /// A rider's month is theirs (docs/adr/0006). Another rider's analysis is neither readable nor
    /// deletable — and "not deletable" has to be a miss rather than a refusal, or the answer itself
    /// tells them the analysis exists.
    /// </summary>
    [Fact]
    public async Task One_riders_analysis_is_invisible_to_another()
    {
        await ResetAsync();
        var mine = await WriteAsync(2026, 6);

        Assert.Null(await WithServiceAsync(s => s.ReadAsync(OtherRider, 2026, 6, AnalysisLanguage.Hungarian)));
        Assert.False(await WithServiceAsync(s => s.DeleteAsync(OtherRider, mine.Analysis!.Id)));
        Assert.NotNull(await WithServiceAsync(s => s.ReadAsync(Rider, 2026, 6, AnalysisLanguage.Hungarian)));
    }
}

/// <summary>
/// Configured off rather than switched off: the switch is on, and there is still no way to write
/// anything, because there is no key. Both have to close the door, or the section renders a button
/// that answers with an exception.
/// </summary>
public class MonthlyAnalysisWithoutAKeyTests(KeylessAnalysisApiFactory factory)
    : IClassFixture<KeylessAnalysisApiFactory>
{
    [Fact]
    public async Task With_no_key_configured_the_feature_is_unavailable_however_the_switch_stands()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMonthlyAnalysisService>();
        await service.SetAvailableAsync(true);

        Assert.False(await service.IsAvailableAsync());

        var outcome = await service.WriteAsync("admin-1", 2026, 6, AnalysisLanguage.Hungarian);

        Assert.Equal(AnalysisRefusal.Unavailable, outcome.Refusal);
        Assert.Empty(factory.Analyst.Asked);
    }
}
