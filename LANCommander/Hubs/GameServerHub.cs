using LANCommander.Services;
using Microsoft.AspNetCore.SignalR;
using NLog;

namespace LANCommander.Hubs
{
    public class GameServerHub : Hub
    {
        readonly ServerProcessService ServerProcessService;
        public GameServerHub(ServerProcessService serverProcessService) {
            ServerProcessService = serverProcessService;

            ServerProcessService.OnLog += ServerProcessService_OnLog;
        }

        private void ServerProcessService_OnLog(object sender, ServerLogEventArgs e)
        {
            Clients.All.SendAsync("Log", e.Log.ServerId, e.Log.Id, e.Line);
        }

        public void Log(Guid serverId, string message)
        {
            Clients.All.SendAsync("Log", serverId, message);
        }
    }
}
