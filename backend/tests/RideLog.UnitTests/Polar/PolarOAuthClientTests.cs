using Microsoft.Extensions.Options;
using RideLog.Infrastructure.Polar;

namespace RideLog.UnitTests.Polar;

public class PolarOAuthClientTests
{
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
