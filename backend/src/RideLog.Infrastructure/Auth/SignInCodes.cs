using System.Collections.Concurrent;
using System.Security.Cryptography;
using RideLog.Application.Auth;

namespace RideLog.Infrastructure.Auth;

/// <summary>
/// Held in memory, which is what "single use" costs: a self-contained signed code would survive a
/// restart but could not be spent, because spending is a fact about the server, not about the code.
/// A restart therefore drops codes still in flight and those riders sign in again — the API is one
/// F1 instance and a code lives seconds, so the trade lands the right way round.
/// </summary>
public sealed class SignInCodes(TimeProvider clock) : ISignInCodes
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, (string RiderId, DateTimeOffset ExpiresAt)> _issued = new();

    public string Issue(string riderId)
    {
        DropExpired();

        var code = Base64Url(RandomNumberGenerator.GetBytes(32));
        _issued[code] = (riderId, clock.GetUtcNow().Add(Lifetime));

        return code;
    }

    public string? Redeem(string code)
    {
        // Removed whether or not it is still valid: a code is spent by being presented.
        if (!_issued.TryRemove(code, out var issued))
        {
            return null;
        }

        return issued.ExpiresAt > clock.GetUtcNow() ? issued.RiderId : null;
    }

    private void DropExpired()
    {
        var now = clock.GetUtcNow();
        foreach (var (code, issued) in _issued)
        {
            if (issued.ExpiresAt <= now)
            {
                _issued.TryRemove(code, out _);
            }
        }
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
