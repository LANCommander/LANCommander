using System.Text;
using LANCommander.Server.Services;

namespace LANCommander.Server.UI.Pages.Servers.Components;

/// <summary>
/// One of a game server's files read as text to edit on its Files page, and written back in the
/// encoding it was read in, byte order mark and all.
/// </summary>
public sealed record ServerTextFile(Guid ServerId, string Path, string Contents, Encoding Encoding)
{
    /// <summary>Larger files are left to be uploaded rather than edited in the browser.</summary>
    public const int MaxEditableBytes = 4 * 1024 * 1024;

    // Text files have no NUL bytes; nearly every binary has one early on
    private const int BinarySniffBytes = 8000;

    private static readonly Encoding DefaultEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>A file that doesn't exist yet, written as UTF-8 when saved.</summary>
    public static ServerTextFile New(Guid serverId, string path) => new(serverId, path, "", DefaultEncoding);

    /// <summary>
    /// Reads one of the server's files. Throws <see cref="InvalidOperationException"/> with a message
    /// for the administrator when it is too large or isn't text.
    /// </summary>
    public static async Task<ServerTextFile> ReadAsync(ServerManager serverManager, Guid serverId, string path, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();

        await using (var stream = await serverManager.OpenFileAsync(serverId, path, cancellationToken))
        {
            var chunk = new byte[81920];
            int read;

            while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
            {
                buffer.Write(chunk, 0, read);

                if (buffer.Length > MaxEditableBytes)
                    throw new InvalidOperationException($"The file is larger than {MaxEditableBytes / 1024 / 1024} MB, so can't be edited here.");
            }
        }

        var bytes = buffer.GetBuffer().AsSpan(0, (int)buffer.Length);

        if (!HasByteOrderMark(bytes) && bytes[..Math.Min(bytes.Length, BinarySniffBytes)].Contains((byte)0))
            throw new InvalidOperationException("The file isn't text, so can't be edited here.");

        buffer.Position = 0;

        using var reader = new StreamReader(buffer, DefaultEncoding, detectEncodingFromByteOrderMarks: true);

        var contents = await reader.ReadToEndAsync(cancellationToken);

        return new ServerTextFile(serverId, path, contents, reader.CurrentEncoding);
    }

    /// <summary>Writes <paramref name="contents"/> to the file in its encoding.</summary>
    public async Task WriteAsync(ServerManager serverManager, string contents, CancellationToken cancellationToken = default)
    {
        using var stream = new MemoryStream();

        stream.Write(Encoding.GetPreamble());
        stream.Write(Encoding.GetBytes(contents));
        stream.Position = 0;

        await serverManager.WriteFileAsync(ServerId, Path, stream, cancellationToken);
    }

    /// <summary>The editor's language for the file, by its extension.</summary>
    public string Language => System.IO.Path.GetExtension(Path).ToLowerInvariant() switch
    {
        ".json" => "json",
        ".xml" or ".config" => "xml",
        ".yml" or ".yaml" => "yaml",
        ".ini" or ".cfg" or ".conf" or ".properties" or ".toml" => "ini",
        ".lua" => "lua",
        ".js" => "javascript",
        ".py" => "python",
        ".ps1" or ".psm1" => "powershell",
        ".sh" => "shell",
        ".bat" or ".cmd" => "bat",
        ".html" or ".htm" => "html",
        ".css" => "css",
        ".md" => "markdown",
        _ => "plaintext",
    };

    // UTF-16 and UTF-32 text is full of NULs, but marked as such
    private static bool HasByteOrderMark(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF])
        || bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xFE])
        || bytes.StartsWith((ReadOnlySpan<byte>)[0xFE, 0xFF]);
}
