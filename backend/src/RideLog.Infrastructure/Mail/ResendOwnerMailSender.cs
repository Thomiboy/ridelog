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
        if (!response.IsSuccessStatusCode)
        {
            // The reason travels in the body, and a refused send is swallowed by the caller (the
            // stored message is the net), so the log line is the only place it can appear. Throwing
            // on the status alone reports "it failed" and loses *why* — e.g. that an unverified
            // sender may only mail the account's own address, which is a settings fix, not an outage.
            var reason = await ReadReasonAsync(response, cancellationToken);
            throw new HttpRequestException(
                $"Resend refused the message ({(int)response.StatusCode} {response.StatusCode}): {reason}",
                inner: null,
                response.StatusCode);
        }
    }

    /// <summary>The provider's explanation, trimmed to keep one bad response from flooding the log.</summary>
    private static async Task<string> ReadReasonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        string body;
        try
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            return $"the response body could not be read ({ex.Message})";
        }

        body = body.Trim();
        if (body.Length == 0)
        {
            return "no reason given";
        }

        return body.Length > MaxReasonLength ? body[..MaxReasonLength] + "…" : body;
    }

    private const int MaxReasonLength = 500;

    private sealed record ResendEmail(string From, string[] To, string Subject, string Text, string? ReplyTo);
}
