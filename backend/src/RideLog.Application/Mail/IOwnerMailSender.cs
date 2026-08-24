namespace RideLog.Application.Mail;

/// <summary>
/// Sends mail to the owner, and only to the owner (#168, docs/adr/0007). The recipient is deliberately
/// absent from the signature: the owner's address comes from configuration, so "nothing here can reach
/// a rider" is a fact the compiler keeps rather than a rule someone remembers. A rider is a cold
/// address on a domain nobody here owns — no SPF or DKIM — so those messages land in spam, and an
/// approval notice that sometimes arrives is worse than none. Mailing anyone else is a new method and
/// a new ADR, not a <c>to</c> argument passed on a Tuesday.
/// </summary>
public interface IOwnerMailSender
{
    /// <summary>
    /// Mails the owner. <paramref name="replyTo"/> is the visitor's own (unverified) address, so a
    /// reply from the owner's client goes back to them — a false one sends it elsewhere, which is
    /// worth knowing and not worth guarding.
    /// </summary>
    Task NotifyOwnerAsync(string subject, string body, string? replyTo = null, CancellationToken cancellationToken = default);
}
