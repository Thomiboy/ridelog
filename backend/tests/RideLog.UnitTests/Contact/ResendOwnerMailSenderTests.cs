using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RideLog.Infrastructure.Mail;
using RideLog.UnitTests.Polar;

namespace RideLog.UnitTests.Contact;

/// <summary>
/// The real sender's wire contract — the seam the faked <c>IOwnerMailSender</c> in the endpoint tests
/// cannot see. It has to reach Resend's <c>/emails</c> with the key, put the owner (and only the
/// owner) in <c>to</c>, and map the visitor's address to <c>reply_to</c>.
/// </summary>
public class ResendOwnerMailSenderTests
{
    [Fact]
    public async Task It_posts_to_resend_with_the_owner_as_recipient_and_the_visitor_as_reply_to()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json("{\"id\":\"abc\"}"));
        var sender = new ResendOwnerMailSender(new HttpClient(handler), Options.Create(new MailOptions
        {
            ApiKey = "re_test_key",
            ApiBaseUrl = "https://api.resend.com",
            FromAddress = "onboarding@resend.dev",
            OwnerAddress = "owner@ridelog.test",
        }));

        await sender.NotifyOwnerAsync("Subject line", "The message body", "visitor@example.test");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.resend.com/emails", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("re_test_key", request.Headers.Authorization.Parameter);

        using var body = JsonDocument.Parse(Assert.Single(handler.RequestBodies)!);
        var root = body.RootElement;
        Assert.Equal("onboarding@resend.dev", root.GetProperty("from").GetString());
        Assert.Equal("owner@ridelog.test", Assert.Single(root.GetProperty("to").EnumerateArray()).GetString());
        Assert.Equal("Subject line", root.GetProperty("subject").GetString());
        Assert.Equal("The message body", root.GetProperty("text").GetString());
        Assert.Equal("visitor@example.test", root.GetProperty("reply_to").GetString());
    }

    [Fact]
    public async Task With_no_reply_to_the_field_is_omitted_rather_than_sent_null()
    {
        var handler = new MockHttpMessageHandler(_ => MockHttpMessageHandler.Json("{\"id\":\"abc\"}"));
        var sender = new ResendOwnerMailSender(new HttpClient(handler), Options.Create(new MailOptions
        {
            ApiKey = "re_test_key",
            OwnerAddress = "owner@ridelog.test",
        }));

        await sender.NotifyOwnerAsync("Subject", "Body");

        using var body = JsonDocument.Parse(Assert.Single(handler.RequestBodies)!);
        Assert.False(body.RootElement.TryGetProperty("reply_to", out _));
    }
}
