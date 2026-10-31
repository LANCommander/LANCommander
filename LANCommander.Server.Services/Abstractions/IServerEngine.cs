using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Services.Abstractions;

public interface IServerEngine
{
    public Task InitializeAsync();
    public Task RefreshTrackingAsync();
    public bool IsManaging(Guid serverId);
    public Task StartAsync(Guid serverId);
    public Task StopAsync(Guid serverId);
    public Task<ServerProcessStatus> GetStatusAsync(Guid serverId);
    public event EventHandler<ServerStatusUpdateEventArgs> OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs> OnServerLog;

    /// <summary>
    /// Samples the server's process (pid, start time, CPU, memory). Null when the server isn't
    /// running or the engine can't observe its process.
    /// </summary>
    public Task<ServerProcessInfo?> GetProcessInfoAsync(Guid serverId) => Task.FromResult<ServerProcessInfo?>(null);

    /// <summary>Whether <see cref="SendInputAsync"/> can currently deliver a line to the server's standard input.</summary>
    public bool CanSendInput(Guid serverId) => false;

    /// <summary>Writes a line to the server's standard input. Returns false if that isn't possible.</summary>
    public Task<bool> SendInputAsync(Guid serverId, string line) => Task.FromResult(false);

    // The server's files. Paths come normalized by ServerFilePath: relative, "/"-separated, "" for the top.

    /// <summary>Whether the engine can manage the server's files.</summary>
    public bool SupportsFiles(Guid serverId) => false;

    /// <summary>Lists a folder of the server's files.</summary>
    public Task<IReadOnlyList<ServerFile>> ListFilesAsync(Guid serverId, string path, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This server's engine can't manage its files.");

    /// <summary>Opens one of the server's files to read; the caller disposes the stream.</summary>
    public Task<Stream> OpenFileAsync(Guid serverId, string path, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This server's engine can't manage its files.");

    /// <summary>Writes one of the server's files, replacing it and creating the folders it is in.</summary>
    public Task WriteFileAsync(Guid serverId, string path, Stream content, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This server's engine can't manage its files.");

    /// <summary>Deletes one of the server's files, or a folder and everything in it.</summary>
    public Task DeleteFileAsync(Guid serverId, string path, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This server's engine can't manage its files.");

    /// <summary>Creates a folder among the server's files.</summary>
    public Task CreateDirectoryAsync(Guid serverId, string path, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("This server's engine can't manage its files.");
}
