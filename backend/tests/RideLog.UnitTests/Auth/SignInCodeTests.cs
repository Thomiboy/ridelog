using RideLog.Infrastructure.Auth;

namespace RideLog.UnitTests.Auth;

/// <summary>
/// The provider's callback hands the browser a code, not a token: this app is cookie-free across two
/// origins, and a JWT in a URL survives in browser history long after the sign-in — on a shared
/// machine that loses accounts. The frontend exchanges the code for the token (docs/adr/0007).
/// </summary>
public class SignInCodeTests
{
    private sealed class MovableClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now = _now.Add(by);
    }

    private static MovableClock Clock() => new(new DateTimeOffset(2026, 8, 8, 10, 0, 0, TimeSpan.Zero));

    [Fact]
    public void A_code_is_exchangeable_exactly_once()
    {
        var codes = new SignInCodes(Clock());
        var code = codes.Issue("rider-1");

        var first = codes.Redeem(code);
        var second = codes.Redeem(code);

        Assert.Equal("rider-1", first);
        Assert.Null(second);
    }

    /// <summary>
    /// A code lives for the seconds between the redirect landing and the frontend posting it back.
    /// Left valid, one read out of a shared machine's history or a proxy log stays usable.
    /// </summary>
    [Fact]
    public void A_code_expires()
    {
        var clock = Clock();
        var codes = new SignInCodes(clock);
        var code = codes.Issue("rider-1");

        clock.Advance(TimeSpan.FromMinutes(3));

        Assert.Null(codes.Redeem(code));
    }

    /// <summary>Two riders signing in at once must not be handed the same code.</summary>
    [Fact]
    public void Each_code_stands_for_the_rider_it_was_issued_for()
    {
        var codes = new SignInCodes(Clock());

        var mine = codes.Issue("rider-1");
        var theirs = codes.Issue("rider-2");

        Assert.NotEqual(mine, theirs);
        Assert.Equal("rider-2", codes.Redeem(theirs));
        Assert.Equal("rider-1", codes.Redeem(mine));
    }
}
