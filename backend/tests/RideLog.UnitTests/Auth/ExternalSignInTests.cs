using Microsoft.Extensions.DependencyInjection;
using RideLog.Application.Auth;

namespace RideLog.UnitTests.Auth;

/// <summary>
/// Run against the real Identity stack rather than a stand-in, because the decision this implements
/// rests on what Identity actually does: `RequireUniqueEmail` is what makes a second account for a
/// known address impossible, and a fake would happily allow the thing being ruled out (docs/adr/0007).
/// </summary>
public class ExternalSignInTests(RideLogApiFactory factory) : IClassFixture<RideLogApiFactory>
{
    private static ExternalIdentity Google(string email, bool verified = true) =>
        new("Google", $"google-{email}", email, verified);

    private static ExternalIdentity Microsoft(string email, bool verified = true) =>
        new("Microsoft", $"microsoft-{email}", email, verified);

    private IExternalSignIn SignIn(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IExternalSignIn>();

    [Fact]
    public async Task A_first_sign_in_makes_a_rider_and_a_second_finds_the_same_one()
    {
        using var scope = factory.Services.CreateScope();
        var identity = Google("first-timer@example.test");

        var first = await SignIn(scope).SignInAsync(identity);
        var second = await SignIn(scope).SignInAsync(identity);

        Assert.NotNull(first);
        Assert.Equal(first.RiderId, second!.RiderId);
    }

    /// <summary>
    /// A rider who used Google today and Microsoft in six months presents the same address, and
    /// `RequireUniqueEmail` leaves no third option: either the second provider attaches to the rider
    /// who already holds that address, or that sign-in fails with nothing the rider can do about it.
    /// </summary>
    [Fact]
    public async Task A_second_provider_carrying_the_same_verified_address_reaches_the_same_rider()
    {
        using var scope = factory.Services.CreateScope();
        const string Address = "both-providers@example.test";

        var viaGoogle = await SignIn(scope).SignInAsync(Google(Address));
        var viaMicrosoft = await SignIn(scope).SignInAsync(Microsoft(Address));

        Assert.Equal(viaGoogle!.RiderId, viaMicrosoft!.RiderId);
    }

    /// <summary>
    /// The sharp end of attaching by address: a provider that will hand over an address it never
    /// checked is a way into somebody else's log for anyone who can put that address into a token.
    /// Refused, and the refusal is what stops it — this rider is reached by a verified sign-in
    /// afterwards, so the address was never the problem.
    /// </summary>
    [Fact]
    public async Task An_address_the_provider_did_not_verify_is_refused()
    {
        using var scope = factory.Services.CreateScope();
        const string Address = "already-a-rider@example.test";
        var rider = await SignIn(scope).SignInAsync(Google(Address));

        var refused = await SignIn(scope).SignInAsync(Microsoft(Address, verified: false));
        var allowed = await SignIn(scope).SignInAsync(Microsoft(Address));

        Assert.Null(refused);
        Assert.Equal(rider!.RiderId, allowed!.RiderId);
    }
}
