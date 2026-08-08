using Microsoft.AspNetCore.Identity;
using RideLog.Application.Auth;

namespace RideLog.Infrastructure.Auth;

internal sealed class AuthService(
    UserManager<IdentityUser> userManager,
    IJwtTokenService tokenService) : IAuthService
{
    public async Task<AccessToken?> LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.CheckPasswordAsync(user, password))
        {
            return null;
        }

        return await TokenFor(user);
    }

    public async Task<AccessToken?> TokenForAsync(string riderId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(riderId);
        return user is null ? null : await TokenFor(user);
    }

    private async Task<AccessToken> TokenFor(IdentityUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        return tokenService.CreateToken(user.Id, user.Email ?? string.Empty, [.. roles]);
    }
}
