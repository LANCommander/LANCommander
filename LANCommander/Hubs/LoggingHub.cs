using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Hubs
{
    public class LoggingHub : Hub
    {
        public void Log(string logMessage)
        {
            Clients.All.SendAsync("Log", logMessage);
        }
    }
}
