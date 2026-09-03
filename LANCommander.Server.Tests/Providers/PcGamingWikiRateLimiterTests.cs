using System.Diagnostics;
using System.Net;
using LANCommander.Server.Services.Providers.Metadata;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace LANCommander.Server.Tests.Providers;

/// <summary>
/// Covers the pacing we do around PCGamingWiki's 60 requests/minute limit. Going over it blocks
/// the whole server's IP for a minute, so this is worth holding still.
/// </summary>
public class PcGamingWikiRateLimiterTests
{
    [Fact]
    public async Task LimiterLetsAFullWindowThroughWithoutWaiting()
    {
        var limiter = new PcGamingWikiRateLimiter();
        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < PcGamingWikiRateLimiter.RequestsPerWindow; i++)
            await limiter.WaitForSlotAsync();

        stopwatch.Stop();

        // Nothing should block until the window is actually full.
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task LimiterBlocksTheRequestThatWouldExceedTheWindow()
    {
        var limiter = new PcGamingWikiRateLimiter();

        for (var i = 0; i < PcGamingWikiRateLimiter.RequestsPerWindow; i++)
            await limiter.WaitForSlotAsync();

        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

        // The next slot can't open until the oldest request ages out a minute from now, so this
        // has to still be waiting when the token trips.
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await limiter.WaitForSlotAsync(cancellation.Token));
    }

    [Fact]
    public async Task HandlerRetriesOnceAfterA429AndHonoursRetryAfter()
    {
        var inner = new StubHandler(
            _ =>
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return response;
            },
            _ => new HttpResponseMessage(HttpStatusCode.OK));

        var handler = new PcGamingWikiThrottlingHandler(
            new PcGamingWikiRateLimiter(),
            NullLogger<PcGamingWikiThrottlingHandler>.Instance)
        {
            InnerHandler = inner
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };

        var stopwatch = Stopwatch.StartNew();
        var result = await client.GetAsync("w/api.php");
        stopwatch.Stop();

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        inner.Calls.ShouldBe(2);

        // It waited rather than retrying immediately, and used Retry-After instead of the
        // 60 second default.
        stopwatch.Elapsed.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(900));
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task HandlerGivesUpAfterASecond429()
    {
        var inner = new StubHandler(
            _ => Throttled(),
            _ => Throttled(),
            _ => new HttpResponseMessage(HttpStatusCode.OK));

        var handler = new PcGamingWikiThrottlingHandler(
            new PcGamingWikiRateLimiter(),
            NullLogger<PcGamingWikiThrottlingHandler>.Instance)
        {
            InnerHandler = inner
        };

        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") };

        var result = await client.GetAsync("w/api.php");

        // One retry, not a loop — repeatedly hammering a rate limiter extends the block.
        result.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        inner.Calls.ShouldBe(2);

        static HttpResponseMessage Throttled()
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromMilliseconds(1));
            return response;
        }
    }

    private sealed class StubHandler(params Func<HttpRequestMessage, HttpResponseMessage>[] responses) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var index = Math.Min(Calls, responses.Length - 1);

            Calls++;

            return Task.FromResult(responses[index](request));
        }
    }
}
