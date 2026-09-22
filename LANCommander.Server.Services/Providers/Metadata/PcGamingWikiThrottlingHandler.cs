using System.Net;
using Microsoft.Extensions.Logging;

namespace LANCommander.Server.Services.Providers.Metadata;

/// <summary>
/// Tracks how many requests we've sent to PCGamingWiki recently.
/// <para>
/// Registered as a singleton and shared by every <see cref="PcGamingWikiThrottlingHandler"/>,
/// because <see cref="IHttpClientFactory"/> recycles its handlers on a timer and the window has to
/// outlive them to mean anything.
/// </para>
/// </summary>
public sealed class PcGamingWikiRateLimiter
{
    /// <summary>
    /// PCGamingWiki's documented limit is 60 requests per minute. We leave headroom because the
    /// limit is enforced against a window we can't observe, and an overrun costs a 60 second block
    /// on the whole server's IP rather than just the request that tripped it.
    /// </summary>
    internal const int RequestsPerWindow = 50;

    internal static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly Queue<DateTimeOffset> _sent = new();

    /// <summary>
    /// Blocks until sending another request keeps us under <see cref="RequestsPerWindow"/> for the
    /// trailing <see cref="Window"/>, then records the send.
    /// </summary>
    public async Task WaitForSlotAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            TimeSpan wait;

            await _gate.WaitAsync(cancellationToken);

            try
            {
                var now = DateTimeOffset.UtcNow;
                var cutoff = now - Window;

                while (_sent.Count > 0 && _sent.Peek() <= cutoff)
                    _sent.Dequeue();

                if (_sent.Count < RequestsPerWindow)
                {
                    _sent.Enqueue(now);
                    return;
                }

                // The oldest request in the window is the first to age out, so that's the earliest
                // a slot can open up.
                wait = _sent.Peek() - cutoff;
            }
            finally
            {
                _gate.Release();
            }

            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, cancellationToken);
        }
    }
}

/// <summary>
/// Keeps our PCGamingWiki traffic inside their published rate limit.
/// <para>
/// Going over the limit returns HTTP 429 and blocks the server's IP for a full minute, which takes
/// out every metadata lookup rather than just the one that tripped it. We pace ourselves below the
/// limit and, if we still get a 429, wait it out once instead of hammering into a longer block.
/// </para>
/// </summary>
public class PcGamingWikiThrottlingHandler(
    PcGamingWikiRateLimiter limiter,
    ILogger<PcGamingWikiThrottlingHandler> logger) : DelegatingHandler
{
    /// <summary>How long to wait out a 429 when the response carries no Retry-After.</summary>
    private static readonly TimeSpan DefaultRetryAfter = TimeSpan.FromSeconds(60);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        await limiter.WaitForSlotAsync(cancellationToken);

        var response = await base.SendAsync(request, cancellationToken);

        if (response.StatusCode != HttpStatusCode.TooManyRequests)
            return response;

        var retryAfter = GetRetryAfter(response);

        logger.LogWarning(
            "PCGamingWiki returned 429 Too Many Requests for {Uri}. Waiting {Seconds}s before a single retry.",
            request.RequestUri, retryAfter.TotalSeconds);

        response.Dispose();

        await Task.Delay(retryAfter, cancellationToken);
        await limiter.WaitForSlotAsync(cancellationToken);

        var retried = await base.SendAsync(request, cancellationToken);

        if (retried.StatusCode == HttpStatusCode.TooManyRequests)
            logger.LogWarning(
                "PCGamingWiki is still rate limiting us after waiting. Giving up on {Uri}.",
                request.RequestUri);

        return retried;
    }

    private static TimeSpan GetRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;

        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero)
            return delta;

        if (retryAfter?.Date is { } date)
        {
            var untilDate = date - DateTimeOffset.UtcNow;

            if (untilDate > TimeSpan.Zero)
                return untilDate;
        }

        return DefaultRetryAfter;
    }
}
