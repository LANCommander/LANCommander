using LANCommander.SDK;
using LANCommander.Services;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Hubs
{
    public class LoggingHub : Hub
    {
        public async Task Log(string message)
        {
            await Clients.All.SendAsync("Log", message);
        }
    }
}
