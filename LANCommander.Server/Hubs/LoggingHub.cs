using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    public class LoggingHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.SendAsync("Log", "Connected to server logging provider!", LogLevel.Information, DateTime.Now);
            await base.OnConnectedAsync();
        }

        public static async Task Log(IHubContext<LoggingHub> context, string message, LogLevel logLevel, DateTime timestamp)
        {
            await context.Clients.All.SendAsync("Log", message, logLevel, timestamp);
        }
    }
}