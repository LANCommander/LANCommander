using Microsoft.AspNetCore.SignalR;
using NLog;

namespace LANCommander.Hubs
{
    public class GameServerHub : Hub
    {
        public void Log(Guid serverId, string message)
        {
            Clients.All.SendAsync("Log", serverId, message);
        }
    }
}
