using System;
using System.IO;
using System.Net.Http;

namespace LANCommander.SDK;

/// <summary>
/// A stream to keep track of progress, probably from Http
/// </summary>
public class TrackableStream : Stream
{
    public TrackableStream(Stream stream, long length)
    {
        _stream = stream;
        _length = length;
    }
    
    private Stream _stream;
    private long _length;
    private long _position;

    public delegate void OnProgressDelegate(long Position, long Length);
    public event OnProgressDelegate OnProgress = delegate { };
    
    public override void Flush()
        => _stream.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var bytesRead = _stream.Read(buffer, offset, count);
        
        _position += bytesRead;
        
        OnProgress?.Invoke(_position, _length);
        
        return bytesRead;
    }

    public override long Seek(long offset, SeekOrigin origin)
        => _stream.Seek(offset, origin);

    public override void SetLength(long value)
        => _stream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count)
        => _stream.Write(buffer, offset, count);

    public override bool CanRead => _stream.CanRead;
    public override bool CanSeek => _stream.CanSeek;
    public override bool CanWrite => _stream.CanWrite;
    public override long Length => _length;
    public override long Position
    {
        get => _position;
        set => throw new NotImplementedException();
    }
}