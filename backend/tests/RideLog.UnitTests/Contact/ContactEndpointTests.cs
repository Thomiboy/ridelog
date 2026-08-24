using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RideLog.Application.Auth;
using RideLog.Application.Mail;
using RideLog.Infrastructure.Persistence;
using RideLog.UnitTests.Auth;

namespace RideLog.UnitTests.Contact;

/// <summary>
/// The contact form: a stranger writes a row, a mail carries it to the owner, and the stored copy is
/// the net under a failed send. Every seam here is the HTTP boundary; the sender is faked, because
/// it is the external service and the point is what the endpoint asks of it, not that Resend answers.
/// </summary>
public sealed class ContactApiFactory : RideLogApiFactory
{
    /// <summary>Stands in for the owner-mail sender: records what it was asked to send, or throws.</summary>
    public sealed class RecordingMailSender : IOwnerMailSender
    {
        public bool Throws { get; set; }
        public List<(string Subject, string Body, string? ReplyTo)> Sent { get; } = [];

        public Task NotifyOwnerAsync(
            string subject, string body, string? replyTo = null, CancellationToken cancellationToken = default)
        {
            if (Throws)
            {
                throw new HttpRequestException("mail provider unreachable");
            }

            Sent.Add((subject, body, replyTo));
            return Task.CompletedTask;
        }
    }

    public RecordingMailSender Mail { get; } = new();

    protected override void ConfigureExtraServices(IServiceCollection services)
    {
        services.RemoveAll<IOwnerMailSender>();
        services.AddSingleton<IOwnerMailSender>(Mail);
    }
}

public class ContactEndpointTests(ContactApiFactory factory) : IClassFixture<ContactApiFactory>
{
    private sealed record LoginRequest(string Email, string Password);
    private sealed record LoginResponse(string Token, DateTimeOffset ExpiresAt);
    private sealed record MessageDto(Guid Id, string SenderName, string SenderEmail, string Message, DateTimeOffset SubmittedAt);
    private sealed record ContactConfigDto(bool Enabled, string? OwnerEmail);

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/auth/login", new LoginRequest(RideLogApiFactory.AdminEmail, RideLogApiFactory.AdminPassword));
        response.EnsureSuccessStatusCode();
        var token = (await response.Content.ReadFromJsonAsync<LoginResponse>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<IReadOnlyList<MessageDto>> MessagesAsync() =>
        (await (await AdminClientAsync()).GetFromJsonAsync<IReadOnlyList<MessageDto>>("/messages"))!;

    [Fact]
    public async Task A_submitted_message_is_stored_and_mailed_with_the_visitor_as_reply_to()
    {
        factory.Mail.Sent.Clear();

        var response = await factory.CreateClient().PostAsJsonAsync("/contact", new
        {
            name = "Wandering Visitor",
            email = "visitor@example.test",
            message = "Loved the route maps — are the climbs categorised?",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // The mail carries the message in full, with the visitor's address as reply-to.
        var sent = Assert.Single(factory.Mail.Sent);
        Assert.Contains("Loved the route maps — are the climbs categorised?", sent.Body);
        Assert.Contains("Wandering Visitor", sent.Body);
        Assert.Equal("visitor@example.test", sent.ReplyTo);

        // And the stored copy is readable by the owner.
        var stored = Assert.Single(
            await MessagesAsync(), m => m.SenderEmail == "visitor@example.test");
        Assert.Equal("Wandering Visitor", stored.SenderName);
        Assert.Equal("Loved the route maps — are the climbs categorised?", stored.Message);
    }

    /// <summary>
    /// Store-first: the send is a separate step from the save, so a provider outage must not take the
    /// visitor's message with it. The visitor is not told it failed — the message is safe, and the
    /// owner finds it in the list even though no mail arrived.
    /// </summary>
    [Fact]
    public async Task A_failed_send_still_leaves_the_message_stored_and_the_visitor_untold()
    {
        factory.Mail.Throws = true;
        try
        {
            var response = await factory.CreateClient().PostAsJsonAsync("/contact", new
            {
                name = "Unlucky Sender",
                email = "outage@example.test",
                message = "The mail provider is down when this arrives.",
            });

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(await MessagesAsync(), m => m.SenderEmail == "outage@example.test");
        }
        finally
        {
            factory.Mail.Throws = false;
        }
    }

    /// <summary>
    /// The kill switch is enforced at the endpoint, not by hiding the form: a bot posts straight to
    /// the endpoint and never loads the page, so turning it off has to refuse the submission itself.
    /// </summary>
    [Fact]
    public async Task With_the_switch_off_a_submission_that_bypasses_the_page_is_refused()
    {
        var admin = await AdminClientAsync();
        (await admin.PutAsJsonAsync("/contact/switch", new { enabled = false })).EnsureSuccessStatusCode();
        try
        {
            var response = await factory.CreateClient().PostAsJsonAsync("/contact", new
            {
                name = "Bot Bypass",
                email = "bypass@example.test",
                message = "Posted straight to the endpoint with the form hidden.",
            });

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.DoesNotContain(await MessagesAsync(), m => m.SenderEmail == "bypass@example.test");
        }
        finally
        {
            (await AdminClientAsync()).PutAsJsonAsync("/contact/switch", new { enabled = true }).GetAwaiter().GetResult();
        }
    }

    /// <summary>
    /// The honeypot: a real visitor never fills the hidden field, so a filled one is a bot. The
    /// submission is dropped — silently, so the bot is not told which field gave it away — and nothing
    /// is stored or mailed.
    /// </summary>
    [Fact]
    public async Task A_filled_honeypot_is_dropped_without_storing_or_mailing()
    {
        factory.Mail.Sent.Clear();

        var response = await factory.CreateClient().PostAsJsonAsync("/contact", new
        {
            name = "Spam Bot",
            email = "honeypot@example.test",
            message = "buy cheap things",
            website = "http://spam.example",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(await MessagesAsync(), m => m.SenderEmail == "honeypot@example.test");
        Assert.Empty(factory.Mail.Sent);
    }

    /// <summary>
    /// The length caps are enforced at the endpoint, not left to the column. A message past the cap is
    /// refused outright, and nothing is stored.
    /// </summary>
    [Fact]
    public async Task A_message_past_the_length_cap_is_refused()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/contact", new
        {
            name = "Overlong",
            email = "toolong@example.test",
            message = new string('x', 5001),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.DoesNotContain(await MessagesAsync(), m => m.SenderEmail == "toolong@example.test");
    }

    /// <summary>The owner reads a message where the mail already put it, then clears it from the list.</summary>
    [Fact]
    public async Task The_owner_can_delete_a_stored_message()
    {
        await factory.CreateClient().PostAsJsonAsync("/contact", new
        {
            name = "To Be Deleted",
            email = "delete-me@example.test",
            message = "This one gets cleared.",
        });
        var admin = await AdminClientAsync();
        var target = (await MessagesAsync()).Single(m => m.SenderEmail == "delete-me@example.test");

        var deleted = await admin.DeleteAsync($"/messages/{target.Id}");
        var again = await admin.DeleteAsync($"/messages/{target.Id}");

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.DoesNotContain(await MessagesAsync(), m => m.Id == target.Id);
    }

    /// <summary>
    /// The list, the kill switch and delete reach across nobody's own log — they are the owner's, so
    /// an ordinary signed-in rider is refused all three. Submitting stays public; reading does not.
    /// </summary>
    [Fact]
    public async Task An_ordinary_rider_cannot_reach_the_owner_only_surface()
    {
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Rider>>();
        const string email = "ordinary-contact@example.test";
        var rider = await users.FindByEmailAsync(email);
        if (rider is null)
        {
            rider = new Rider { UserName = email, Email = email, EmailConfirmed = true, Approval = Approval.Approved };
            await users.CreateAsync(rider);
        }

        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenService>().CreateToken(rider.Id, email, []);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        var listed = await client.GetAsync("/messages");
        var switched = await client.PutAsJsonAsync("/contact/switch", new { enabled = false });
        var deleted = await client.DeleteAsync($"/messages/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, listed.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, switched.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, deleted.StatusCode);
    }

    /// <summary>
    /// While the form works, the page shows the form — so the config says so and withholds the owner's
    /// address, which is not for scraping when there is a form to use instead.
    /// </summary>
    [Fact]
    public async Task The_config_reports_the_form_on_and_withholds_the_owner_address()
    {
        (await (await AdminClientAsync()).PutAsJsonAsync("/contact/switch", new { enabled = true })).EnsureSuccessStatusCode();

        var config = await factory.CreateClient().GetFromJsonAsync<ContactConfigDto>("/contact");

        Assert.True(config!.Enabled);
        Assert.Null(config.OwnerEmail);
    }

    /// <summary>
    /// With the form off the page still works: the form disappears and the owner's address takes its
    /// place, so the config has to hand that address over now that there is no form.
    /// </summary>
    [Fact]
    public async Task With_the_form_off_the_config_offers_the_owner_address()
    {
        var admin = await AdminClientAsync();
        (await admin.PutAsJsonAsync("/contact/switch", new { enabled = false })).EnsureSuccessStatusCode();
        try
        {
            var config = await factory.CreateClient().GetFromJsonAsync<ContactConfigDto>("/contact");

            Assert.False(config!.Enabled);
            Assert.Equal(RideLogApiFactory.OwnerEmail, config.OwnerEmail);
        }
        finally
        {
            (await AdminClientAsync()).PutAsJsonAsync("/contact/switch", new { enabled = true }).GetAwaiter().GetResult();
        }
    }
}
