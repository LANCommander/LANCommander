using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    public class GameServerHub : Hub
    {
        readonly ServerManager _serverManager;

        public GameServerHub(ServerManager serverManager) {
            _serverManager = serverManager;
        }

        public async Task GetStatus(Guid serverId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Server/{serverId}");

            await UpdateStatusAsync(serverId);
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

        public void Log(Guid serverId, string message)
        {
            Clients.All.SendAsync("Log", serverId, message);
        }
    }
}
