using LANCommander.Server.Services;
using LANCommander.Server.Services.Abstractions;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    public class GameServerHub : Hub
    {
        readonly IEnumerable<IServerEngine> _serverEngines;
        readonly ServerService _serverService;
        
        public GameServerHub(
            IServiceProvider serviceProvider,
            ServerService serverService) {
            _serverEngines = serviceProvider.GetServices<IServerEngine>();
            _serverService = serverService;
        }

        public async Task GetStatus(Guid serverId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Server/{serverId}");

            await UpdateStatusAsync(serverId);
        }

        public async Task UpdateStatusAsync(Guid serverId)
        {
            foreach (var serverEngine in _serverEngines)
            {
                if (serverEngine.IsManaging(serverId))
                    await Clients.All.SendAsync("StatusUpdate", await serverEngine.GetStatusAsync(serverId), serverId);
            }
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
            foreach (var serverEngine in _serverEngines)
            {
                if (serverEngine.IsManaging(serverId))
                    Task.Run(() => serverEngine.StartAsync(serverId));
            }
        }

        public async Task StopServer(Guid serverId)
        {
            foreach (var serverEngine in _serverEngines)
            {
                if (serverEngine.IsManaging(serverId))
                    Task.Run(() => serverEngine.StopAsync(serverId));
            }
        }

        public void Log(Guid serverId, string message)
        {
            Clients.All.SendAsync("Log", serverId, message);
        }
    }
}
