using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    public class GameServerHub : Hub
    {
        readonly ServerProcessService _serverProcessService;
        readonly ServerService _serverService;
        
        public GameServerHub(
            ServerProcessService serverProcessService,
            ServerService serverService) {
            _serverProcessService = serverProcessService;
            _serverService = serverService;
        }

        public async Task GetStatus(Guid serverId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Server/{serverId}");

            await UpdateStatusAsync(serverId);
        }

        public async Task UpdateStatusAsync(Guid serverId)
        {
            var status = _serverProcessService.GetStatus(serverId);
        
            await Clients.All.SendAsync("StatusUpdate", status, serverId);
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
            Task.Run(() => _serverProcessService.StartServerAsync(serverId));
        }

        public async Task StopServer(Guid serverId)
        {
            Task.Run(() => _serverProcessService.StopServerAsync(serverId));
        }

        public void Log(Guid serverId, string message)
        {
            Clients.All.SendAsync("Log", serverId, message);
        }
    }
}
