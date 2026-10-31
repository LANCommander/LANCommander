using System.Text;
using LANCommander.Server.Data.Models;

namespace LANCommander.Server.Services.Utilities;

/// <summary>
/// Follows a log file the way <c>tail -F</c> does, passing each line written to it after the monitor
/// started to <c>onLine</c>. Lines already in the file are left alone; the Monitor reads those itself.
/// </summary>
/// <remarks>
/// The file is polled rather than watched: <see cref="FileSystemWatcher"/> misses changes on network
/// shares and in bind mounts, and many servers keep the file open and write to it without the change
/// being signalled until the handle is flushed. A file that doesn't exist yet is waited for; one that is
/// truncated, or deleted or rotated away, is read again from its start.
/// </remarks>
public sealed class LogFileMonitor : IAsyncDisposable
{
    public static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(250);

    private readonly string _path;
    private readonly Action<string> _onLine;
    private readonly TimeSpan _pollInterval;
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _loop;

    private readonly Decoder _decoder = new UTF8Encoding(false).GetDecoder();
    private readonly StringBuilder _pending = new();
    private readonly byte[] _buffer = new byte[16 * 1024];
    private char[] _chars = new char[UTF8Encoding.UTF8.GetMaxCharCount(16 * 1024)];

    private FileStream? _stream;

    // Where reading continues; null until the file has first been looked at
    private long? _position;

    public LogFileMonitor(string path, Action<string> onLine, TimeSpan? pollInterval = null)
    {
        _path = Path.GetFullPath(path);
        _onLine = onLine;
        _pollInterval = pollInterval ?? DefaultPollInterval;

        // The file's current end is where following starts, so what was there before isn't repeated
        _position = File.Exists(_path) ? new FileInfo(_path).Length : 0;

        _loop = Task.Run(RunAsync);
    }

    /// <summary>The file a log file console reads: its path, under the server's working directory unless absolute.</summary>
    public static string ResolvePath(Data.Models.Server server, ServerConsole console) =>
        Path.Combine(server.WorkingDirectory ?? "", console.Path ?? "");

    /// <summary>The file followed.</summary>
    public string FilePath => _path;

    private async Task RunAsync()
    {
        while (!_stopping.IsCancellationRequested)
        {
            ReadAvailable();

            try
            {
                await Task.Delay(_pollInterval, _stopping.Token);
            }
            catch (OperationCanceledException)
            {
            }
        }

        // Whatever was written up to the moment the monitor was stopped, e.g. the server's last words
        ReadAvailable();
        Flush();
        Close();
    }

    private void ReadAvailable()
    {
        try
        {
            if (_stream == null && !Open())
                return;

            var length = _stream!.Length;

            // Truncated, e.g. the server cleared it when starting again
            if (length < _position)
                Restart();
            // Deleted or renamed away (rotated): what was written to it has been read, so move on to the
            // file now at the path, if there is one
            else if (length == _position && Replaced())
            {
                Flush();
                Close();
                _position = 0;

                if (!Open())
                    return;
            }

            int read;

            while ((read = _stream!.Read(_buffer, 0, _buffer.Length)) > 0)
            {
                _position += read;
                Decode(read);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Locked or gone for the moment; try again next time
            Close();
        }
    }

    private bool Open()
    {
        if (!File.Exists(_path))
        {
            // Created later, so all of it is new
            _position = 0;
            return false;
        }

        _stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        if (_position > _stream.Length)
            _position = 0;

        _stream.Position = _position ?? 0;

        return true;
    }

    /// <summary>Whether the path no longer names the file being read.</summary>
    private bool Replaced()
    {
        var current = new FileInfo(_path);

        if (!current.Exists)
            return true;

        // A new file shorter than what has been read can't be the same one
        return current.Length < _position;
    }

    private void Restart()
    {
        Flush();

        _position = 0;
        _stream!.Position = 0;
        _decoder.Reset();
    }

    private void Decode(int count)
    {
        var decoded = _decoder.GetChars(_buffer, 0, count, _chars, 0);

        for (var i = 0; i < decoded; i++)
        {
            var c = _chars[i];

            if (c == '\n')
                Emit();
            else if (c != '\r')
                _pending.Append(c);
        }
    }

    private void Flush()
    {
        if (_pending.Length > 0)
            Emit();
    }

    private void Emit()
    {
        var line = _pending.ToString();

        _pending.Clear();

        try
        {
            _onLine(line);
        }
        catch
        {
            // A failing listener mustn't stop the file being followed
        }
    }

    private void Close()
    {
        _stream?.Dispose();
        _stream = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_stopping.IsCancellationRequested)
            return;

        await _stopping.CancelAsync();
        await _loop;

        _stopping.Dispose();
    }
}
