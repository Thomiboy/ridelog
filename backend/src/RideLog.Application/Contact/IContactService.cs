namespace RideLog.Application.Contact;

/// <summary>What a visitor submitted through the contact form (the honeypot is dropped at the edge).</summary>
public sealed record ContactSubmission(string Name, string Email, string Message);

/// <summary>One stored contact message, for the owner's small <c>/messages</c> list.</summary>
public sealed record ContactMessageDto(Guid Id, string SenderName, string SenderEmail, string Message, DateTimeOffset SubmittedAt);

/// <summary>
/// What the public contact page needs to render itself: whether the form is on, and — only when it is
/// off — the owner's address to show in its place. The address is withheld while there is a form to
/// use, since it is not for scraping (#168).
/// </summary>
public sealed record ContactConfig(bool Enabled, string? OwnerEmail);

/// <summary>
/// The contact form's back end: store a visitor's message and notify the owner, list what has come in,
/// and delete. Reading is deliberately a flat list with delete rather than a threaded inbox — the mail
/// carries the message, so the owner reads and replies where they already are (#168).
/// </summary>
public interface IContactService
{
    /// <summary>Stores the message, then mails it to the owner. Store-first: a failed send keeps the copy.</summary>
    Task SubmitAsync(ContactSubmission submission, CancellationToken cancellationToken = default);

    /// <summary>Every stored message, most recent first.</summary>
    Task<IReadOnlyList<ContactMessageDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Removes one stored message; false when there is no such message.</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Whether the form is accepting submissions. Unset (never toggled) means on.</summary>
    Task<bool> IsAcceptingAsync(CancellationToken cancellationToken = default);

    /// <summary>The kill switch: turns submissions on or off. Stored, so it survives a restart (#168).</summary>
    Task SetAcceptingAsync(bool accepting, CancellationToken cancellationToken = default);

    /// <summary>What the public contact page renders from: the switch, and the owner address when off.</summary>
    Task<ContactConfig> GetConfigAsync(CancellationToken cancellationToken = default);
}
