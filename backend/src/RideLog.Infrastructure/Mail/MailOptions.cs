namespace RideLog.Infrastructure.Mail;

/// <summary>
/// Mail settings, bound from the "Mail" configuration section. Targets Resend's HTTP API: it sends
/// from an onboarding sender with no verified domain, and when a domain arrives it is verified and
/// <see cref="FromAddress"/> changes — one setting, not a provider swap (#168).
/// </summary>
public sealed class MailOptions
{
    public const string SectionName = "Mail";

    public string ApiKey { get; set; } = string.Empty;
    public string ApiBaseUrl { get; set; } = "https://api.resend.com";

    /// <summary>The sender address. Resend's onboarding sender works until a domain is verified.</summary>
    public string FromAddress { get; set; } = "onboarding@resend.dev";

    /// <summary>Where owner notifications go — and the address the contact page shows when it is off.</summary>
    public string OwnerAddress { get; set; } = string.Empty;
}
