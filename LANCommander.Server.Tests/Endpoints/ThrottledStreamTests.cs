using System.Diagnostics;
using LANCommander.Server.Endpoints;
using Shouldly;

namespace LANCommander.Server.Tests.Endpoints;

public class ThrottledStreamTests
{
    [Fact]
    public async Task UnlimitedStreamReadsAllBytesWithoutDelay()
    {
        var data = new byte[256 * 1024];
        Random.Shared.NextBytes(data);

        using var inner = new MemoryStream(data);
        await using var throttled = new ThrottledStream(inner, 0);

        var output = new MemoryStream();
        var stopwatch = Stopwatch.StartNew();
        await throttled.CopyToAsync(output, 16 * 1024);
        stopwatch.Stop();

        output.ToArray().ShouldBe(data);
        stopwatch.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ThrottledStreamLimitsThroughput()
    {
        // 200 KB at 100 KB/s should take at least ~1 second (one full window beyond the first).
        const int rate = 100 * 1024;
        var data = new byte[200 * 1024];

        using var inner = new MemoryStream(data);
        await using var throttled = new ThrottledStream(inner, rate);

        var output = new MemoryStream();
        var stopwatch = Stopwatch.StartNew();
        await throttled.CopyToAsync(output, 16 * 1024);
        stopwatch.Stop();

        output.Length.ShouldBe(data.Length);
        stopwatch.Elapsed.ShouldBeGreaterThan(TimeSpan.FromMilliseconds(800));
    }

    [Fact]
    public void LengthAndSeekAreDelegatedToInner()
    {
        var data = new byte[1024];
        using var inner = new MemoryStream(data);
        using var throttled = new ThrottledStream(inner, 1024);

        throttled.Length.ShouldBe(data.Length);
        throttled.CanSeek.ShouldBeTrue();

        throttled.Seek(100, SeekOrigin.Begin);
        throttled.Position.ShouldBe(100);
        inner.Position.ShouldBe(100);
    }
}
