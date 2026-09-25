#nullable enable
using System;
using System.Buffers.Binary;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.PowerShell.Debugging.Remote;

/// <summary>
/// Length-prefixed JSON message framing over a duplex stream: a little-endian int32 payload length
/// followed by that many bytes of UTF-8 JSON. A read either produces a whole message or fails.
/// </summary>
public sealed class DebugMessageChannel : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly byte[] _lengthBuffer = new byte[4];

    private bool _disposed;

    public DebugMessageChannel(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        _leaveOpen = leaveOpen;
    }

    /// <summary>Writes one message. Safe to call concurrently; frames never interleave.</summary>
    public async Task WriteAsync(DebugMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = JsonSerializer.SerializeToUtf8Bytes(message, DebugProtocol.SerializerOptions);

        if (payload.Length > DebugProtocol.MaxFrameLength)
            throw new InvalidOperationException(
                $"Message of {payload.Length} bytes exceeds the {DebugProtocol.MaxFrameLength} byte frame limit.");

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var header = new byte[4];

            BinaryPrimitives.WriteInt32LittleEndian(header, payload.Length);

            await _stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    /// <summary>Reads one message, or returns null when the peer closed the stream.</summary>
    /// <exception cref="InvalidDataException">The stream is out of sync and cannot be recovered.</exception>
    public async Task<DebugMessage?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!await ReadExactlyAsync(_lengthBuffer, cancellationToken).ConfigureAwait(false))
            return null;

        var length = BinaryPrimitives.ReadInt32LittleEndian(_lengthBuffer);

        if (length <= 0 || length > DebugProtocol.MaxFrameLength)
            throw new InvalidDataException($"Invalid frame length {length}; the channel is out of sync.");

        var payload = new byte[length];

        if (!await ReadExactlyAsync(payload, cancellationToken).ConfigureAwait(false))
            return null;

        return JsonSerializer.Deserialize<DebugMessage>(payload, DebugProtocol.SerializerOptions);
    }

    /// <summary>
    /// Fills the buffer, or returns false if the stream ended first. A partial read at the end of a
    /// stream is a clean disconnect: a process being killed mid-frame is an expected condition.
    /// </summary>
    private async Task<bool> ReadExactlyAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        var offset = 0;

        while (offset < buffer.Length)
        {
            var read = await _stream
                .ReadAsync(buffer.AsMemory(offset, buffer.Length - offset), cancellationToken)
                .ConfigureAwait(false);

            if (read == 0)
                return false;

            offset += read;
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _writeLock.Dispose();

        if (!_leaveOpen)
            _stream.Dispose();
    }
}
