using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LANCommander.SDK.Services;

/// <summary>
/// A read-only stream that presents an ordered list of files as a single continuous stream.
/// Used to reassemble chunked pack files transparently.
/// </summary>
public class ConcatenatingStream : Stream
{
    private readonly IReadOnlyList<string> _filePaths;
    private readonly IReadOnlyList<long> _skipBytes;
    private int _currentIndex;
    private Stream? _currentStream;
    private long _position;
    private long _length;

    /// <param name="filePaths">Ordered list of file paths to concatenate.</param>
    /// <param name="skipBytes">
    /// Number of bytes to skip at the start of each file (e.g., chunk header size).
    /// Must have the same count as filePaths. Use 0 for no skip.
    /// </param>
    public ConcatenatingStream(IReadOnlyList<string> filePaths, IReadOnlyList<long> skipBytes)
    {
        if (filePaths.Count != skipBytes.Count)
            throw new ArgumentException("filePaths and skipBytes must have the same count.");

        _filePaths = filePaths;
        _skipBytes = skipBytes;
        _currentIndex = 0;

        _length = 0;
        for (int i = 0; i < filePaths.Count; i++)
        {
            var fileInfo = new FileInfo(filePaths[i]);
            _length += fileInfo.Length - skipBytes[i];
        }
    }

    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => throw new NotSupportedException("ConcatenatingStream does not support seeking.");
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        var totalRead = 0;

        while (totalRead < count)
        {
            if (_currentStream == null)
            {
                if (!OpenNextStream())
                    break;
            }

            var bytesRead = _currentStream!.Read(buffer, offset + totalRead, count - totalRead);

            if (bytesRead == 0)
            {
                _currentStream.Dispose();
                _currentStream = null;

                continue;
            }

            totalRead += bytesRead;
            _position += bytesRead;
        }

        return totalRead;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var totalRead = 0;

        while (totalRead < count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_currentStream == null)
            {
                if (!OpenNextStream())
                    break;
            }

            var bytesRead = await _currentStream!.ReadAsync(buffer.AsMemory(offset + totalRead, count - totalRead), cancellationToken);

            if (bytesRead == 0)
            {
                _currentStream.Dispose();
                _currentStream = null;

                continue;
            }

            totalRead += bytesRead;
            _position += bytesRead;
        }

        return totalRead;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        var totalRead = 0;

        while (totalRead < buffer.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_currentStream == null)
            {
                if (!OpenNextStream())
                    break;
            }

            var bytesRead = await _currentStream!.ReadAsync(buffer.Slice(totalRead), cancellationToken);

            if (bytesRead == 0)
            {
                _currentStream.Dispose();
                _currentStream = null;

                continue;
            }

            totalRead += bytesRead;
            _position += bytesRead;
        }

        return totalRead;
    }

    private bool OpenNextStream()
    {
        if (_currentIndex >= _filePaths.Count)
            return false;

        _currentStream = new FileStream(_filePaths[_currentIndex], FileMode.Open, FileAccess.Read, FileShare.Read);

        var skip = _skipBytes[_currentIndex];
        if (skip > 0)
            _currentStream.Seek(skip, SeekOrigin.Begin);

        _currentIndex++;
        return true;
    }

    public override void Flush() { }

    public override long Seek(long offset, SeekOrigin origin)
        => throw new NotSupportedException("ConcatenatingStream does not support seeking.");

    public override void SetLength(long value)
        => throw new NotSupportedException("ConcatenatingStream is read-only.");

    public override void Write(byte[] buffer, int offset, int count)
        => throw new NotSupportedException("ConcatenatingStream is read-only.");

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _currentStream?.Dispose();
            _currentStream = null;
        }

        base.Dispose(disposing);
    }
}
