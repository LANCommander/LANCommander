using LANCommander.SDK;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    public class LoggingHub : Hub
    {
        public async Task Log(string message)
        {
            await Clients.All.SendAsync("Log", message);
        }
    }
}
