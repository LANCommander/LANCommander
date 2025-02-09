using LANCommander.SDK;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Serilog.Events;

namespace LANCommander.Server.Hubs
{
    public class LoggingHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            Clients.Caller.SendAsync("Log", "Connected to server logging provider!", LogEventLevel.Information, DateTime.Now);
            await base.OnConnectedAsync();
        }

        public static async Task Log(IHubContext<LoggingHub> context, string message, LogEvent logEvent)
        {

            await context.Clients.All.SendAsync("Log", message, logEvent.Level, logEvent.Timestamp.DateTime);
        }
    }
}