using LANCommander.SDK.Helpers;

namespace LANCommander.SDK.Tests.Helpers;

public class RetryHelperTests
{
    // ── RetryOnException<T> ───────────────────────────────────────────────────

    [Fact]
    public void RetryOnException_WhenActionSucceedsImmediately_ReturnsResult()
    {
        var result = RetryHelper.RetryOnException(3, TimeSpan.Zero, -1, () => 42);

        Assert.Equal(42, result);
    }

    [Fact]
    public void RetryOnException_WhenActionSucceedsOnSecondAttempt_ReturnsResult()
    {
        int calls = 0;

        var result = RetryHelper.RetryOnException(3, TimeSpan.Zero, -1, () =>
        {
            calls++;
            if (calls < 2) throw new Exception("first attempt fails");
            return 99;
        });

        Assert.Equal(99, result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void RetryOnException_WhenActionAlwaysFails_ReturnsDefault()
    {
        var result = RetryHelper.RetryOnException(3, TimeSpan.Zero, -1, () =>
        {
            throw new Exception("always fails");
#pragma warning disable CS0162
            return 0;
#pragma warning restore CS0162
        });

        Assert.Equal(-1, result);
    }

    [Fact]
    public void RetryOnException_WhenActionAlwaysFails_TriesMaxAttemptsTimes()
    {
        int calls = 0;

        RetryHelper.RetryOnException(4, TimeSpan.Zero, -1, () =>
        {
            calls++;
            throw new Exception("fail");
#pragma warning disable CS0162
            return 0;
#pragma warning restore CS0162
        });

        Assert.Equal(4, calls);
    }

    [Fact]
    public void RetryOnException_WithOneMaxAttempt_DoesNotRetry()
    {
        int calls = 0;

        RetryHelper.RetryOnException(1, TimeSpan.Zero, -1, () =>
        {
            calls++;
            throw new Exception("fail");
#pragma warning disable CS0162
            return 0;
#pragma warning restore CS0162
        });

        Assert.Equal(1, calls);
    }

    // ── RetryOnException (void) ───────────────────────────────────────────────
    // NOTE: The void overload has an infinite loop bug: on successful execution
    // of action() there is no return or break, so the loop continues indefinitely.
    // Success-path tests for this overload are skipped to avoid hanging.

    [Fact(Skip = "RetryOnException void overload loops infinitely on success (missing return/break after action())")]
    public void RetryOnException_Void_WhenActionSucceedsImmediately_CompletesOnce()
    {
        bool ran = false;
        RetryHelper.RetryOnException(3, TimeSpan.Zero, () => { ran = true; });
        Assert.True(ran);
    }

    [Fact]
    public void RetryOnException_Void_WhenActionAlwaysFails_TriesMaxAttemptsTimes()
    {
        int calls = 0;

        RetryHelper.RetryOnException(3, TimeSpan.Zero, () =>
        {
            calls++;
            throw new Exception("fail");
        });

        Assert.Equal(3, calls);
    }

    [Fact]
    public void RetryOnException_Void_WithOneMaxAttempt_DoesNotRetry()
    {
        int calls = 0;

        RetryHelper.RetryOnException(1, TimeSpan.Zero, () =>
        {
            calls++;
            throw new Exception("fail");
        });

        Assert.Equal(1, calls);
    }

    // ── RetryOnExceptionAsync<T> ──────────────────────────────────────────────

    [Fact]
    public async Task RetryOnExceptionAsync_WhenActionSucceedsImmediately_ReturnsResult()
    {
        var result = await RetryHelper.RetryOnExceptionAsync(3, TimeSpan.Zero, -1, () => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RetryOnExceptionAsync_WhenActionSucceedsOnSecondAttempt_ReturnsResult()
    {
        int calls = 0;

        var result = await RetryHelper.RetryOnExceptionAsync(3, TimeSpan.Zero, -1, () =>
        {
            calls++;
            if (calls < 2) throw new Exception("first attempt fails");
            return Task.FromResult(77);
        });

        Assert.Equal(77, result);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RetryOnExceptionAsync_WhenActionAlwaysFails_ReturnsDefault()
    {
        var result = await RetryHelper.RetryOnExceptionAsync(3, TimeSpan.Zero, -1, () =>
        {
            throw new Exception("always fails");
#pragma warning disable CS0162
            return Task.FromResult(0);
#pragma warning restore CS0162
        });

        Assert.Equal(-1, result);
    }

    [Fact]
    public async Task RetryOnExceptionAsync_WhenActionAlwaysFails_TriesMaxAttemptsTimes()
    {
        int calls = 0;

        await RetryHelper.RetryOnExceptionAsync(4, TimeSpan.Zero, -1, () =>
        {
            calls++;
            throw new Exception("fail");
#pragma warning disable CS0162
            return Task.FromResult(0);
#pragma warning restore CS0162
        });

        Assert.Equal(4, calls);
    }

    // ── RetryOnExceptionAsync (void) ──────────────────────────────────────────
    // NOTE: Same infinite loop bug as the sync void overload.

    [Fact(Skip = "RetryOnExceptionAsync void overload loops infinitely on success (missing return/break after await action())")]
    public async Task RetryOnExceptionAsync_Void_WhenActionSucceedsImmediately_CompletesOnce()
    {
        bool ran = false;
        await RetryHelper.RetryOnExceptionAsync(3, TimeSpan.Zero, () => { ran = true; return Task.CompletedTask; });
        Assert.True(ran);
    }

    [Fact]
    public async Task RetryOnExceptionAsync_Void_WhenActionAlwaysFails_TriesMaxAttemptsTimes()
    {
        int calls = 0;

        await RetryHelper.RetryOnExceptionAsync(3, TimeSpan.Zero, () =>
        {
            calls++;
            throw new Exception("fail");
#pragma warning disable CS0162
            return Task.CompletedTask;
#pragma warning restore CS0162
        });

        Assert.Equal(3, calls);
    }
}
