using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using RideLog.Infrastructure.Polar;

namespace RideLog.UnitTests.Polar;

public class PolarOAuthClientTests
{
    private static PolarOAuthClient NewClient(MockHttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(new PolarOptions
        {
            ClientId = "client-123",
            ClientSecret = "secret-456",
            RedirectUri = "https://ridelog.test/polar/callback",
            TokenUrl = "https://polarremote.test/v2/oauth2/token",
            ApiBaseUrl = "https://api.polar.test",
        }));

    [Fact]
    public async Task Exchange_registers_the_member_with_a_kebab_case_member_id()
    {
        // Polar's JSON is kebab-case everywhere; /v3/users expects "member-id".
        var handler = new MockHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/token")
                ? MockHttpMessageHandler.Json("""{ "access_token": "tok-1", "x_user_id": 4242 }""")
                : MockHttpMessageHandler.Status(HttpStatusCode.OK));

        var token = await NewClient(handler).ExchangeCodeAsync("auth-code");

        Assert.Equal("tok-1", token.AccessToken);
        Assert.Equal("4242", token.PolarUserId);

        Assert.Equal("https://api.polar.test/v3/users", handler.Requests[1].RequestUri!.ToString());
        var body = handler.RequestBodies[1]!;
        Assert.Contains("\"member-id\"", body);
        Assert.DoesNotContain("member_id", body);
    }

    [Fact]
    public async Task Exchange_tolerates_an_already_registered_member()
    {
        var handler = new MockHttpMessageHandler(request =>
            request.RequestUri!.AbsolutePath.EndsWith("/token")
                ? MockHttpMessageHandler.Json("""{ "access_token": "tok-1", "x_user_id": 4242 }""")
                : MockHttpMessageHandler.Status(HttpStatusCode.Conflict));

        var token = await NewClient(handler).ExchangeCodeAsync("auth-code");

        Assert.Equal("tok-1", token.AccessToken);
    }

    [Fact]
    public void Authorize_url_carries_the_client_id_redirect_and_state()
    {
        var options = Options.Create(new PolarOptions
        {
            ClientId = "client-123",
            RedirectUri = "https://ridelog.test/polar/callback",
            AuthorizeUrl = "https://flow.polar.com/oauth2/authorization",
        });
        var client = new PolarOAuthClient(new HttpClient(), options);

        var url = client.BuildAuthorizeUrl("state-xyz");

        Assert.StartsWith("https://flow.polar.com/oauth2/authorization?", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("client_id=client-123", url);
        Assert.Contains("redirect_uri=https%3A%2F%2Fridelog.test%2Fpolar%2Fcallback", url);
        Assert.Contains("state=state-xyz", url);
    }
}
