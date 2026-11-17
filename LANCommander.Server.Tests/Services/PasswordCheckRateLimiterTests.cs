using LANCommander.Server.Plugins;
using Shouldly;

namespace LANCommander.Server.Tests.Services;

public class PasswordCheckRateLimiterTests
{
    [Fact]
    public void AUserNameIsLimitedAfterTooManyFailures()
    {
        var clock = new ManualTimeProvider();
        var limiter = new PasswordCheckRateLimiter(clock);

        for (var i = 0; i < PasswordCheckRateLimiter.MaxFailures; i++)
        {
            limiter.TryStart("alice").ShouldBeNull();
            clock.Advance(TimeSpan.FromSeconds(10));
        }

        // The first failure was 50 seconds ago, so it expires a window after that
        limiter.TryStart("ALICE").ShouldBe(PasswordCheckRateLimiter.Window - TimeSpan.FromSeconds(50));

        // Other user names are unaffected
        limiter.TryStart("bob").ShouldBeNull();
    }

    [Fact]
    public void FailuresExpireOneAtATime()
    {
        var clock = new ManualTimeProvider();
        var limiter = new PasswordCheckRateLimiter(clock);

        for (var i = 0; i < PasswordCheckRateLimiter.MaxFailures; i++)
        {
            limiter.TryStart("alice");
            clock.Advance(TimeSpan.FromSeconds(10));
        }

        clock.Advance(PasswordCheckRateLimiter.Window - TimeSpan.FromSeconds(50));

        limiter.TryStart("alice").ShouldBeNull();
        limiter.TryStart("alice").ShouldNotBeNull();
    }

    [Fact]
    public void ASuccessForgivesEarlierFailures()
    {
        var limiter = new PasswordCheckRateLimiter(new ManualTimeProvider());

        for (var i = 0; i < PasswordCheckRateLimiter.MaxFailures - 1; i++)
            limiter.TryStart("alice");

        limiter.TryStart("alice").ShouldBeNull();
        limiter.Succeeded("alice");

        for (var i = 0; i < PasswordCheckRateLimiter.MaxFailures; i++)
            limiter.TryStart("alice").ShouldBeNull();
    }

    sealed class ManualTimeProvider : TimeProvider
    {
        DateTimeOffset _now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan by) => _now += by;
    }
}
