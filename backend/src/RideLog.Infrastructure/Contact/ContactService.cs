using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RideLog.Application.Contact;
using RideLog.Application.Mail;
using RideLog.Application.Settings;
using RideLog.Domain.Contact;
using RideLog.Infrastructure.Mail;
using RideLog.Infrastructure.Persistence;

namespace RideLog.Infrastructure.Contact;

internal sealed class ContactService(
    RideLogDbContext context,
    IOwnerMailSender mail,
    ISettingsStore settings,
    IOptions<MailOptions> mailOptions,
    TimeProvider clock,
    ILogger<ContactService> logger) : IContactService
{
    public async Task SubmitAsync(ContactSubmission submission, CancellationToken cancellationToken = default)
    {
        // Store first, then send: a third-party call that fails must not take the visitor's message
        // with it (#168, echoing docs/adr/0005 — the send is a separate step from the save).
        var message = new ContactMessage
        {
            Id = Guid.NewGuid(),
            SenderName = submission.Name,
            SenderEmail = submission.Email,
            Body = submission.Message,
            SubmittedAt = clock.GetUtcNow(),
        };
        context.ContactMessages.Add(message);
        await context.SaveChangesAsync(cancellationToken);

        // The send is best-effort on top of the stored copy: a provider outage is logged and swallowed
        // so the visitor still sees success and the message is not lost. Send-and-forget has no moment
        // where the message is safe; this does — the row is already committed above.
        var subject = $"RideLog contact — {submission.Name}";
        var body = $"From: {submission.Name} <{submission.Email}>\n\n{submission.Message}";
        try
        {
            await mail.NotifyOwnerAsync(subject, body, submission.Email, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Contact message {MessageId} was stored but the owner mail failed to send.", message.Id);
        }
    }

    public async Task<IReadOnlyList<ContactMessageDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Ordered after materializing: SQLite (the test provider) cannot ORDER BY a DateTimeOffset,
        // and the owner's message list is small enough that sorting it in memory costs nothing.
        var messages = await context.ContactMessages
            .Select(message => new ContactMessageDto(
                message.Id, message.SenderName, message.SenderEmail, message.Body, message.SubmittedAt))
            .ToListAsync(cancellationToken);

        return messages.OrderByDescending(message => message.SubmittedAt).ToList();
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var removed = await context.ContactMessages
            .Where(message => message.Id == id)
            .ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }

    public async Task<bool> IsAcceptingAsync(CancellationToken cancellationToken = default)
    {
        // Unset means on: the form works out of the box, and only an explicit "false" turns it off.
        var stored = await settings.GetAsync(SettingsKeys.ContactFormEnabled, cancellationToken);
        return stored is null || bool.Parse(stored);
    }

    public Task SetAcceptingAsync(bool accepting, CancellationToken cancellationToken = default) =>
        settings.SetAsync(SettingsKeys.ContactFormEnabled, accepting ? "true" : "false", cancellationToken);

    public async Task<ContactConfig> GetConfigAsync(CancellationToken cancellationToken = default)
    {
        // The owner's address goes out only when the form is off and the page needs a fallback; while
        // there is a form to use, it is withheld rather than handed to every visitor (and scraper).
        var enabled = await IsAcceptingAsync(cancellationToken);
        return new ContactConfig(enabled, enabled ? null : mailOptions.Value.OwnerAddress);
    }
}
