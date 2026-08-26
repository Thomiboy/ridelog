using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RideLog.Application.Analysis;
using RideLog.Application.Messaging;
using RideLog.Application.Settings;
using RideLog.Domain.Analysis;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Analysis;

/// <summary>
/// The monthly analysis, end to end (#187).
///
/// Two things here are worth reading twice. The <b>quota is the key</b>: rider, month and language
/// identify an analysis, a closed month never changes, so one is written once and read free from then
/// on — there is no counter to keep and no window to reset. And the store happens <b>after</b> the
/// call, the opposite way round from the contact form (#168): there the visitor's message has value
/// before the send, so it is kept first; here the call's result is the whole value, so a failed call
/// leaves nothing behind and costs only itself.
/// </summary>
internal sealed class MonthlyAnalysisService(
    RideLogDbContext context,
    ISettingsStore settings,
    IConfiguration configuration,
    IDispatcher dispatcher,
    ITrainingAnalyst analyst,
    TimeProvider clock) : IMonthlyAnalysisService
{
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        // Two gates, and both have to open. A key without a switch is a feature the owner never asked
        // to start paying for; a switch without a key is a button that answers with an exception.
        if (string.IsNullOrWhiteSpace(configuration["Ai:ApiKey"]))
        {
            return false;
        }

        var stored = await settings.GetAsync(SettingsKeys.MonthlyAnalysisEnabled, cancellationToken);
        return bool.TryParse(stored, out var enabled) && enabled;
    }

    public Task SetAvailableAsync(bool available, CancellationToken cancellationToken = default) =>
        settings.SetAsync(SettingsKeys.MonthlyAnalysisEnabled, available.ToString(), cancellationToken);

    public async Task<MonthlyAnalysisDto?> ReadAsync(
        string riderId, int year, int month, AnalysisLanguage language, CancellationToken cancellationToken = default)
    {
        var stored = await Find(riderId, year, month, language).FirstOrDefaultAsync(cancellationToken);
        return stored is null ? null : ToDto(stored);
    }

    public async Task<MonthlyAnalysisOutcome> WriteAsync(
        string riderId, int year, int month, AnalysisLanguage language, CancellationToken cancellationToken = default)
    {
        if (!await IsAvailableAsync(cancellationToken))
        {
            return MonthlyAnalysisOutcome.Refused(AnalysisRefusal.Unavailable);
        }

        var summary = await dispatcher.QueryAsync(
            new GetMonthlyTrainingSummaryQuery(riderId, year, month), cancellationToken);

        var existing = await Find(riderId, year, month, language).FirstOrDefaultAsync(cancellationToken);
        if (existing is not null)
        {
            // A closed month is finished: what it holds cannot change, so neither can its reading.
            // The running one may be read again, but only once it has something new to say.
            if (!IsRunning(year, month))
            {
                return MonthlyAnalysisOutcome.Refused(AnalysisRefusal.AlreadyWritten);
            }

            if (existing.RideCount == summary.Month.RideCount)
            {
                return MonthlyAnalysisOutcome.Refused(AnalysisRefusal.Unchanged);
            }

            context.MonthlyAnalyses.Remove(existing);
        }

        var text = await analyst.AnalyseAsync(summary, language, cancellationToken);

        var written = new MonthlyAnalysis
        {
            Id = Guid.NewGuid(),
            UserId = riderId,
            Year = year,
            Month = month,
            Language = language.ToString(),
            Text = text,
            Model = analyst.Model,
            RideCount = summary.Month.RideCount,
            WrittenAt = clock.GetUtcNow(),
        };

        context.MonthlyAnalyses.Add(written);
        await context.SaveChangesAsync(cancellationToken);

        return MonthlyAnalysisOutcome.Written(ToDto(written));
    }

    public async Task<bool> DeleteAsync(string riderId, Guid id, CancellationToken cancellationToken = default)
    {
        // Named by rider as well as by id: somebody else's analysis is not found rather than refused,
        // because a refusal would confirm it exists (docs/adr/0006).
        var stored = await context.MonthlyAnalyses
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == riderId, cancellationToken);
        if (stored is null)
        {
            return false;
        }

        context.MonthlyAnalyses.Remove(stored);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    private IQueryable<MonthlyAnalysis> Find(string riderId, int year, int month, AnalysisLanguage language)
    {
        var name = language.ToString();
        return context.MonthlyAnalyses
            .Where(a => a.UserId == riderId && a.Year == year && a.Month == month && a.Language == name);
    }

    private bool IsRunning(int year, int month)
    {
        var now = clock.GetUtcNow();
        return now.Year == year && now.Month == month;
    }

    private static MonthlyAnalysisDto ToDto(MonthlyAnalysis analysis) => new(
        analysis.Id,
        analysis.Year,
        analysis.Month,
        Enum.Parse<AnalysisLanguage>(analysis.Language),
        analysis.Text,
        analysis.Model,
        analysis.RideCount,
        analysis.WrittenAt);
}
