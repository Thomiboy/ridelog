using Microsoft.AspNetCore.Identity;
using RideLog.Application.Auth;

namespace RideLog.Infrastructure.Auth;

internal sealed class ExternalSignIn(UserManager<IdentityUser> users) : IExternalSignIn
{
    public async Task<ExternalSignInResult?> SignInAsync(
        ExternalIdentity identity,
        CancellationToken cancellationToken = default)
    {
        // Refused before anything is looked up, because the address is what riders are matched on:
        // a provider willing to hand over one it never checked is a way into somebody else's log.
        if (!identity.EmailVerified || string.IsNullOrWhiteSpace(identity.Email))
        {
            return null;
        }

        var known = await users.FindByLoginAsync(identity.Provider, identity.Subject);
        if (known is not null)
        {
            return new ExternalSignInResult(known.Id);
        }

        // A rider who used one provider today and another in six months presents the same address,
        // and `RequireUniqueEmail` will not make a second account for it. So the second provider
        // attaches to the rider who already holds the address (docs/adr/0007).
        var rider = await users.FindByEmailAsync(identity.Email);
        if (rider is null)
        {
            // Confirmed because a refusal above is the only way to get here: the provider vouched
            // for the address, and it is the only verification this app will ever have.
            rider = new IdentityUser { UserName = identity.Email, Email = identity.Email, EmailConfirmed = true };
            var created = await users.CreateAsync(rider);
            if (!created.Succeeded)
            {
                throw new InvalidOperationException(
                    $"Could not create a rider for {identity.Provider}: " +
                    string.Join("; ", created.Errors.Select(error => error.Description)));
            }
        }

        await users.AddLoginAsync(rider, new UserLoginInfo(identity.Provider, identity.Subject, identity.Provider));

        return new ExternalSignInResult(rider.Id);
    }
}
