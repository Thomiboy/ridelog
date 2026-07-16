namespace RideLog.Application.Auth;

/// <summary>Issues signed JWT access tokens for authenticated users.</summary>
public interface IJwtTokenService
{
    AccessToken CreateToken(string userId, string email, IReadOnlyCollection<string> roles);
}

/// <summary>A signed access token and the instant it expires.</summary>
public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);
