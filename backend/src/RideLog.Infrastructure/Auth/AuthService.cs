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

        var roles = await userManager.GetRolesAsync(user);
        return tokenService.CreateToken(user.Id, user.Email ?? email, [.. roles]);
    }
}
