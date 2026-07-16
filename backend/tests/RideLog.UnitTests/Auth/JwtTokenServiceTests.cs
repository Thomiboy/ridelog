using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using RideLog.Application.Auth;
using RideLog.Infrastructure.Auth;

namespace RideLog.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "ridelog-api",
        Audience = "ridelog-web",
        SigningKey = "test-signing-key-at-least-32-bytes-long!!",
        TokenLifetimeMinutes = 30,
    };

    private static IJwtTokenService NewService() =>
        new JwtTokenService(Microsoft.Extensions.Options.Options.Create(Options));

    /// <summary>Validates a token independently of how it was produced, so the assertion can disagree with the code.</summary>
    private static ClaimsPrincipal Validate(string token)
    {
        var parameters = new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SigningKey)),
            ValidateIssuerSigningKey = true,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
        // Match the production JwtBearer config (MapInboundClaims = false): keep the raw JWT claim names.
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        return handler.ValidateToken(token, parameters, out _);
    }

    [Fact]
    public void Token_carries_the_user_id_email_and_roles()
    {
        var result = NewService().CreateToken("admin-1", "admin@ridelog.test", ["Admin"]);

        var principal = Validate(result.Token);

        Assert.Equal("admin-1", principal.FindFirstValue(JwtRegisteredClaimNames.Sub));
        Assert.Equal("admin@ridelog.test", principal.FindFirstValue(JwtRegisteredClaimNames.Email));
        Assert.Contains(principal.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Admin");
    }

    [Fact]
    public void Token_expires_after_the_configured_lifetime()
    {
        var before = DateTimeOffset.UtcNow;

        var result = NewService().CreateToken("admin-1", "admin@ridelog.test", ["Admin"]);

        // Configured lifetime is 30 minutes; allow a small window for execution time.
        Assert.InRange(
            result.ExpiresAt,
            before.AddMinutes(Options.TokenLifetimeMinutes),
            before.AddMinutes(Options.TokenLifetimeMinutes).AddSeconds(30));

        // The returned expiry must match the token's own exp claim (whole seconds).
        var exp = new JwtSecurityTokenHandler().ReadJwtToken(result.Token).ValidTo;
        Assert.Equal(result.ExpiresAt.UtcDateTime, exp, TimeSpan.FromSeconds(1));
    }
}
