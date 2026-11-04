using LANCommander.SDK.Enums;

namespace LANCommander.Server.Plugins;

/// <summary>Where a game server runs.</summary>
public enum ServerHostEngine
{
    /// <summary>A process on the LANCommander server's machine.</summary>
    Local = 0,
    /// <summary>A server managed by another LANCommander server.</summary>
    Remote = 2,
    /// <summary>A plugin's <see cref="IServerEngineExtension"/>, named by <see cref="ServerInfo.EngineExtensionId"/>.</summary>
    Extension = 3,
}

/// <summary>A dedicated game server the LANCommander server runs and monitors.</summary>
public sealed record ServerInfo
{
    /// <summary>Empty when adding a server, to have the LANCommander server choose one.</summary>
    public Guid Id { get; init; }

    public string Name { get; init; } = "";

    /// <summary>The game the server is for, if any.</summary>
    public Guid? GameId { get; init; }

    public ServerHostEngine Engine { get; init; } = ServerHostEngine.Local;

    /// <summary>The executable to run.</summary>
    public string Path { get; init; } = "";

    public string Arguments { get; init; } = "";
    public string WorkingDirectory { get; init; } = "";
    public string OnStartScriptPath { get; init; } = "";
    public string OnStopScriptPath { get; init; } = "";
    public bool UseShellExecute { get; init; }
    public ProcessTerminationMethod ProcessTerminationMethod { get; init; }

    /// <summary>For <see cref="ServerHostEngine.Extension"/>: the engine's <see cref="IServerEngineExtension.Id"/>.</summary>
    public string EngineExtensionId { get; init; } = "";

    /// <summary>For <see cref="ServerHostEngine.Extension"/>: the engine's settings for the server, as JSON.</summary>
    public string EngineSettings { get; init; } = "";

    /// <summary>For <see cref="ServerHostEngine.Remote"/>: the LANCommander server that manages it.</summary>
    public Guid? RemoteHostId { get; init; }

    /// <summary>For <see cref="ServerHostEngine.Remote"/>: the server's id on that LANCommander server.</summary>
    public Guid? RemoteServerId { get; init; }

    /// <summary>The address players connect to.</summary>
    public string Host { get; init; } = "";

    public int Port { get; init; }
    public bool Autostart { get; init; }
    public ServerAutostartMethod AutostartMethod { get; init; }

    /// <summary>Seconds to wait before an automatic start.</summary>
    public int AutostartDelay { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime CreatedOn { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public DateTime UpdatedOn { get; init; }
}

/// <summary>A log file or RCON connection the LANCommander server shows for a game server.</summary>
public sealed record ServerConsoleInfo
{
    public Guid Id { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public Guid ServerId { get; init; }

    public string Name { get; init; } = "";
    public ServerConsoleType Type { get; init; }

    /// <summary>For a log file console: the file.</summary>
    public string Path { get; init; } = "";

    /// <summary>For an RCON console: the host to connect to.</summary>
    public string Host { get; init; } = "";

    /// <summary>For an RCON console: the port to connect to.</summary>
    public int? Port { get; init; }

    /// <summary>For an RCON console: the password.</summary>
    public string Password { get; init; } = "";
}

/// <summary>A folder of the game server the LANCommander server serves over HTTP.</summary>
public sealed record ServerHttpPathInfo
{
    public Guid Id { get; init; }

    /// <summary>Set by the server; ignored when writing.</summary>
    public Guid ServerId { get; init; }

    /// <summary>The folder on disk.</summary>
    public string LocalPath { get; init; } = "";

    /// <summary>The URL path it is served under.</summary>
    public string Path { get; init; } = "";
}

/// <summary>A file or folder returned by <see cref="IServerProvider.ListFilesAsync"/>.</summary>
/// <param name="Name">The entry's name within the listed folder.</param>
/// <param name="Path">The path relative to the engine's file root, with "/" between folders.</param>
/// <param name="IsDirectory">Whether the entry is a folder.</param>
/// <param name="Size">A file's length in bytes, or null when unknown or a folder.</param>
/// <param name="LastModified">The last write time in UTC, or null when unknown.</param>
public sealed record ServerFileInfo(string Name, string Path, bool IsDirectory, long? Size = null, DateTime? LastModified = null);

/// <summary>
/// Lists and manages the server's game servers, with their consoles and HTTP paths. Provided by the
/// server; inject it into a scoped plugin service or page. Their actions and scripts are managed
/// through <see cref="IActionProvider"/> and <see cref="IScriptProvider"/>.
/// Like the other record providers, this is a trusted host service, not an authorization boundary:
/// plugins must protect their pages and endpoints with the appropriate access policy.
/// </summary>
public interface IServerProvider
{
    /// <summary>
    /// Lists one folder through the server's selected host engine. An empty path lists the engine's
    /// file root (the working directory for local servers). Paths are relative, not machine paths;
    /// absolute paths, traversal and malformed names are rejected. No engine failure becomes an empty list.
    /// </summary>
    /// <exception cref="ArgumentException">The path is not a valid relative server file path.</exception>
    /// <exception cref="KeyNotFoundException">The game server does not exist.</exception>
    /// <exception cref="NotSupportedException">The engine does not support files.</exception>
    /// <exception cref="InvalidOperationException">No engine manages the server, or its files are unavailable (for example, a stopped container).</exception>
    /// <exception cref="DirectoryNotFoundException">The folder does not exist.</exception>
    Task<IReadOnlyList<ServerFileInfo>> ListFilesAsync(Guid serverId, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens a file through the selected host engine, using the same relative paths and errors as
    /// <see cref="ListFilesAsync"/>. The caller owns and must dispose the returned stream before its
    /// plugin scope ends. It may be nonseekable; pass cancellation to subsequent reads as well.
    /// </summary>
    /// <exception cref="FileNotFoundException">The file does not exist.</exception>
    /// <exception cref="ArgumentException">The path is invalid or does not name a file.</exception>
    Task<Stream> OpenFileAsync(Guid serverId, string relativePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes a file through the selected host engine, replacing it and creating parent folders.
    /// Uses the same relative paths and errors as <see cref="ListFilesAsync"/>. Reads content from its
    /// current position to the end without seeking or disposing it; the caller retains ownership.
    /// Cancellation or failure may leave a partial file; writes are not atomic.
    /// </summary>
    /// <exception cref="ArgumentException">The path is invalid, does not name a file, or content is not readable.</exception>
    Task WriteFileAsync(Guid serverId, string relativePath, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Every game server, ordered by name.</summary>
    Task<IReadOnlyList<ServerInfo>> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>The game server with the given id, or null.</summary>
    Task<ServerInfo?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">The game does not exist.</exception>
    Task<ServerInfo> AddAsync(ServerInfo server, CancellationToken cancellationToken = default);

    /// <summary>Writes every field of a game server.</summary>
    /// <exception cref="KeyNotFoundException">The game server, or its game, does not exist.</exception>
    Task<ServerInfo> UpdateAsync(ServerInfo server, CancellationToken cancellationToken = default);

    /// <summary>Deletes a game server. Does nothing when there is no such game server.</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such game server.</exception>
    Task<IReadOnlyList<ServerConsoleInfo>> GetConsolesAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such game server.</exception>
    Task<ServerConsoleInfo> AddConsoleAsync(Guid serverId, ServerConsoleInfo console, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such console.</exception>
    Task<ServerConsoleInfo> UpdateConsoleAsync(ServerConsoleInfo console, CancellationToken cancellationToken = default);

    /// <summary>Deletes a console. Does nothing when there is no such console.</summary>
    Task DeleteConsoleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such game server.</exception>
    Task<IReadOnlyList<ServerHttpPathInfo>> GetHttpPathsAsync(Guid serverId, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such game server.</exception>
    Task<ServerHttpPathInfo> AddHttpPathAsync(Guid serverId, ServerHttpPathInfo httpPath, CancellationToken cancellationToken = default);

    /// <exception cref="KeyNotFoundException">There is no such HTTP path.</exception>
    Task<ServerHttpPathInfo> UpdateHttpPathAsync(ServerHttpPathInfo httpPath, CancellationToken cancellationToken = default);

    /// <summary>Deletes an HTTP path. Does nothing when there is no such HTTP path.</summary>
    Task DeleteHttpPathAsync(Guid id, CancellationToken cancellationToken = default);
}
