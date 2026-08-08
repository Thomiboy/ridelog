using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RideLog.Application.Auth;
using RideLog.Infrastructure.Auth;

namespace RideLog.UnitTests.Auth;

/// <summary>
/// The deployment only sets a client id and secret per provider; the authorize and token endpoints
/// come from the defaults in code. Whether binding a dictionary keeps those defaults is a fact about
/// the configuration binder, not something to assume — get it wrong and a provider that looks
/// configured answers 404, which is exactly how this would reach production unnoticed.
/// </summary>
public class ExternalSignInConfigurationTests
{
    private static ExternalSignInOptions Bind(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.Configure<ExternalSignInOptions>(configuration.GetSection(ExternalSignInOptions.SectionName));

        return services.BuildServiceProvider().GetRequiredService<IOptions<ExternalSignInOptions>>().Value;
    }

    private static IExternalProviders Providers(ExternalSignInOptions options) =>
        new ExternalProviders(new HttpClient(), Options.Create(options));

    /// <summary>The shape the README documents, written exactly as App Service spells it.</summary>
    private static readonly Dictionary<string, string?> AsDeployed = new()
    {
        ["ExternalSignIn:RedirectUriTemplate"] = "https://api.test/auth/{provider}/callback",
        ["ExternalSignIn:Providers:google:ClientId"] = "google-client-id",
        ["ExternalSignIn:Providers:google:ClientSecret"] = "google-secret",
        ["ExternalSignIn:Providers:microsoft:ClientId"] = "microsoft-client-id",
        ["ExternalSignIn:Providers:microsoft:ClientSecret"] = "microsoft-secret",
    };

    [Fact]
    public void Setting_only_the_credentials_leaves_the_provider_endpoints_in_place()
    {
        var providers = Providers(Bind(AsDeployed));

        Assert.True(providers.Knows("google"));
        Assert.True(providers.Knows("microsoft"));
        Assert.StartsWith("https://accounts.google.com/o/oauth2/v2/auth?", providers.BuildAuthorizeUrl("google", "s"));
        Assert.StartsWith(
            "https://login.microsoftonline.com/common/oauth2/v2.0/authorize?",
            providers.BuildAuthorizeUrl("microsoft", "s"));
    }

    /// <summary>
    /// Microsoft sends no email_verified claim, so its entry says an absent claim still counts as
    /// verified. Losing that to configuration would refuse every Microsoft rider.
    /// </summary>
    [Fact]
    public void Microsofts_verified_without_the_claim_survives_configuration()
    {
        Assert.True(Bind(AsDeployed).Providers["microsoft"].EmailVerifiedWhenUnstated);
        Assert.False(Bind(AsDeployed).Providers["google"].EmailVerifiedWhenUnstated);
    }

    /// <summary>The redirect the provider consoles have to whitelist, spelled out per provider.</summary>
    [Fact]
    public void The_redirect_is_the_template_with_the_provider_filled_in()
    {
        var url = Providers(Bind(AsDeployed)).BuildAuthorizeUrl("google", "state-value");

        Assert.Contains($"redirect_uri={Uri.EscapeDataString("https://api.test/auth/google/callback")}", url);
    }

    /// <summary>
    /// A provider with no client id is not configured, and answering 404 is what makes that visible
    /// rather than sending a rider to a broken consent screen.
    /// </summary>
    [Fact]
    public void A_provider_without_credentials_is_not_known()
    {
        var providers = Providers(Bind(new Dictionary<string, string?>
        {
            ["ExternalSignIn:RedirectUriTemplate"] = "https://api.test/auth/{provider}/callback",
            ["ExternalSignIn:Providers:google:ClientId"] = "google-client-id",
        }));

        Assert.True(providers.Knows("google"));
        Assert.False(providers.Knows("microsoft"));
    }
}
