using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using RideLog.Application.Mail;

namespace RideLog.Infrastructure.Mail;

/// <summary>
/// Sends owner mail through Resend's HTTP API (POST /emails, bearer key), shaped like the Polar and
/// Open-Meteo clients already here. The recipient is fixed to the configured owner address — the
/// interface takes none (#168).
/// </summary>
internal sealed class ResendOwnerMailSender(HttpClient http, IOptions<MailOptions> options) : IOwnerMailSender
{
    private static readonly JsonSerializerOptions Json = new()
    {
        // Resend reads snake_case, and reply_to is omitted rather than sent null.
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task NotifyOwnerAsync(
        string subject, string body, string? replyTo = null, CancellationToken cancellationToken = default)
    {
        var config = options.Value;
        var payload = new ResendEmail(config.FromAddress, [config.OwnerAddress], subject, body, replyTo);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{config.ApiBaseUrl.TrimEnd('/')}/emails")
        {
            Content = JsonContent.Create(payload, options: Json),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed record ResendEmail(string From, string[] To, string Subject, string Text, string? ReplyTo);
}
