using System.Diagnostics;

namespace LANCommander.Server.Endpoints;

/// <summary>
/// Wraps a source stream and limits how fast it can be read by sleeping when a per-second byte budget is exceeded.
/// Seek and length members are delegated to the inner stream so HTTP range/resume and Content-Length keep working.
/// A <see cref="_bytesPerSecond"/> of 0 (or less) disables throttling.
/// </summary>
internal sealed class ThrottledStream : Stream
{
    private readonly Stream _inner;
    private readonly long _bytesPerSecond;

    private long _windowStartTimestamp;
    private long _bytesInWindow;

    public ThrottledStream(Stream inner, long bytesPerSecond)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _bytesPerSecond = bytesPerSecond;
        _windowStartTimestamp = Stopwatch.GetTimestamp();
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_bytesPerSecond <= 0)
            return await _inner.ReadAsync(buffer, cancellationToken);

        await ThrottleAsync(cancellationToken);

        var read = await _inner.ReadAsync(buffer, cancellationToken);

        Accumulate(read);

        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        return await ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (_bytesPerSecond <= 0)
            return _inner.Read(buffer, offset, count);

        ThrottleAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

        var read = _inner.Read(buffer, offset, count);

        Accumulate(read);

        return read;
    }

    private async ValueTask ThrottleAsync(CancellationToken cancellationToken)
    {
        if (_bytesInWindow < _bytesPerSecond)
            return;

        var elapsed = Stopwatch.GetElapsedTime(_windowStartTimestamp);
        var remaining = TimeSpan.FromSeconds(1) - elapsed;

        if (remaining > TimeSpan.Zero)
            await Task.Delay(remaining, cancellationToken);

        _windowStartTimestamp = Stopwatch.GetTimestamp();
        _bytesInWindow = 0;
    }

    private void Accumulate(int read)
    {
        if (read <= 0)
            return;

        if (Stopwatch.GetElapsedTime(_windowStartTimestamp) >= TimeSpan.FromSeconds(1))
        {
            _windowStartTimestamp = Stopwatch.GetTimestamp();
            _bytesInWindow = 0;
        }

        _bytesInWindow += read;
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => false;
    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void Flush() => _inner.Flush();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();

        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        await _inner.DisposeAsync();
        await base.DisposeAsync();
    }
}
