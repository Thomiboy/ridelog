namespace RideLog.Domain.Contact;

/// <summary>
/// A message a visitor left through the contact form. Stored first, before the mail goes out, so a
/// failed send does not take it with it — the stored copy is the net under the send (#168).
/// </summary>
public class ContactMessage
{
    public Guid Id { get; init; }

    public required string SenderName { get; init; }

    /// <summary>The visitor's own, unverified address; used as the mail's reply-to.</summary>
    public required string SenderEmail { get; init; }

    public required string Body { get; init; }

    public DateTimeOffset SubmittedAt { get; init; }
}
