using LANCommander.SDK.Extensions;

namespace LANCommander.SDK.Tests.Extensions;

public class StreamExtensionsTests
{
    private static MemoryStream MakeStream(int sizeBytes, byte fill = 0xAB)
    {
        var data = new byte[sizeBytes];
        Array.Fill(data, fill);
        return new MemoryStream(data);
    }

    // ── Content correctness ───────────────────────────────────────────────────

    [Fact]
    public async Task CopyToAsync_CopiesAllBytesToDestination()
    {
        var source      = MakeStream(256, fill: 0x42);
        var destination = new MemoryStream();

        await source.CopyToAsync(destination);

        destination.Position = 0;
        var result = destination.ToArray();
        Assert.Equal(256, result.Length);
        Assert.All(result, b => Assert.Equal(0x42, b));
    }

    [Fact]
    public async Task CopyToAsync_EmptySource_ProducesEmptyDestination()
    {
        var source      = new MemoryStream();
        var destination = new MemoryStream();

        await source.CopyToAsync(destination);

        Assert.Equal(0, destination.Length);
    }

    [Fact]
    public async Task CopyToAsync_LargerThanBuffer_CopiesAllBytes()
    {
        // Default buffer is 1 MB; use 3 MB to force multiple reads.
        const int size  = 3 * 1024 * 1024;
        var source      = MakeStream(size, fill: 0x77);
        var destination = new MemoryStream();

        await source.CopyToAsync(destination);

        Assert.Equal(size, destination.Length);
    }

    // ── Progress callback ──────────────────────────────────────────────────────

    [Fact]
    public async Task CopyToAsync_FinalProgressCallback_IsAlwaysInvoked()
    {
        var source      = MakeStream(128);
        var destination = new MemoryStream();

        long lastTransferred = -1;
        long lastTotal       = -1;

        await source.CopyToAsync(destination, (transferred, total) =>
        {
            lastTransferred = transferred;
            lastTotal       = total;
        });

        Assert.Equal(128, lastTransferred);
        Assert.Equal(128, lastTotal);
    }

    [Fact]
    public async Task CopyToAsync_ProgressCallback_IsInvokedAtConfiguredInterval()
    {
        // Use a stream larger than the report interval to trigger mid-copy callbacks.
        const int reportInterval = 64 * 1024;          // 64 KB
        const int streamSize     = 4 * reportInterval; // 256 KB — forces several interval hits

        var source      = MakeStream(streamSize);
        var destination = new MemoryStream();

        var callbackValues = new List<long>();

        await source.CopyToAsync(
            destination,
            progressCallback:    (transferred, _) => callbackValues.Add(transferred),
            bufferSize:          reportInterval,     // each read == one interval
            reportIntervalBytes: reportInterval);

        // At minimum the final callback fires; with buffer == interval every read triggers one.
        Assert.NotEmpty(callbackValues);
        // Last reported value is the total bytes.
        Assert.Equal(streamSize, callbackValues.Last());
    }

    [Fact]
    public async Task CopyToAsync_WithNullProgressCallback_DoesNotThrow()
    {
        var source      = MakeStream(256);
        var destination = new MemoryStream();

        var ex = await Record.ExceptionAsync(() =>
            source.CopyToAsync(destination, progressCallback: null));

        Assert.Null(ex);
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CopyToAsync_WithAlreadyCancelledToken_ThrowsOperationCanceledException()
    {
        var source      = MakeStream(1024 * 1024); // large enough to not finish before cancellation
        var destination = new MemoryStream();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            source.CopyToAsync(destination, cancellationToken: cts.Token));
    }
}
