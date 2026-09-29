using System.Collections.Concurrent;
using LANCommander.Server.Services;
using LANCommander.Server.Services.Models;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    /// <summary>
    /// Game server control and monitoring. Clients call <see cref="Watch"/> (or
    /// <see cref="GetStatus"/>) to join a server's group, which receives "LogLine" and, every two
    /// seconds, "ProcessInfo" pushes from <c>ServerEngineStatusService</c>.
    /// </summary>
    public class GameServerHub : Hub
    {
        readonly ServerManager _serverManager;

        // serverId -> watching connection ids, so process sampling only runs for servers someone is looking at
        private static readonly ConcurrentDictionary<Guid, ConcurrentDictionary<string, byte>> Watchers = new();

        public static string GetGroupName(Guid serverId) => $"Server/{serverId}";

        /// <summary>Servers that at least one connected client is watching.</summary>
        public static IReadOnlyCollection<Guid> WatchedServerIds =>
            Watchers.Where(w => !w.Value.IsEmpty).Select(w => w.Key).ToList();

        public GameServerHub(ServerManager serverManager) {
            _serverManager = serverManager;
        }

        public async Task GetStatus(Guid serverId)
        {
            await Watch(serverId);

            await UpdateStatusAsync(serverId);
        }

        /// <summary>Joins the server's group to receive its "LogLine" and "ProcessInfo" pushes.</summary>
        public async Task Watch(Guid serverId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, GetGroupName(serverId));

            Watchers.GetOrAdd(serverId, _ => new ConcurrentDictionary<string, byte>())[Context.ConnectionId] = 0;
        }

        public async Task Unwatch(Guid serverId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetGroupName(serverId));

            if (Watchers.TryGetValue(serverId, out var connections))
                connections.TryRemove(Context.ConnectionId, out _);
        }

        public async Task UpdateStatusAsync(Guid serverId)
        {
            if (_serverManager.IsManaging(serverId))
                await Clients.All.SendAsync("StatusUpdate", await _serverManager.GetStatusAsync(serverId), serverId);
        }

        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"Client connected: {Context.ConnectionAborted}");
            await Clients.Caller.SendAsync("OnConnected", Context.ConnectionId);

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"Client disconnected {Context.ConnectionId}");

            foreach (var connections in Watchers.Values)
                connections.TryRemove(Context.ConnectionId, out _);

            await base.OnDisconnectedAsync(exception);
        }

        public async Task StartServer(Guid serverId)
        {
            // StartAsync blocks for the server's lifetime, so fire-and-forget.
            _ = Task.Run(() => _serverManager.StartAsync(serverId));
        }

        public async Task StopServer(Guid serverId)
        {
            _ = Task.Run(() => _serverManager.StopAsync(serverId));
        }

        // Sending commands and restarting are deliberately not exposed here: this hub is mapped
        // without authorization (the Blazor pages connect to it server-side without credentials).
        // Pages call ServerManager.SendCommandAsync / RestartAsync directly instead.

        public Task<ServerProcessInfo?> GetProcessInfo(Guid serverId) => _serverManager.GetProcessInfoAsync(serverId);

        public void Log(Guid serverId, string message)
        {
            Clients.All.SendAsync("Log", serverId, message);
        }
    }
}
