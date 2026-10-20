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
}
