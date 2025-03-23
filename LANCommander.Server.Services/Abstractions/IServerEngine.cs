using LANCommander.Server.Services.Enums;
using LANCommander.Server.Services.Models;

namespace LANCommander.Server.Services.Abstractions;

public interface IServerEngine
{
    public Task StartAsync(Guid serverId);
    public Task StopAsync(Guid serverId);
    public Task<ServerProcessStatus> GetStatusAsync(Guid serverId);
    public event EventHandler<ServerStatusUpdateEventArgs> OnServerStatusUpdate;
    public event EventHandler<ServerLogEventArgs> OnServerLog;
}