using LANCommander.SDK;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR;
using Serilog.Events;

namespace LANCommander.Server.Hubs
{
    public class LoggingHub : Hub
    {
        public static async Task Log(IHubContext<LoggingHub> context, string message, LogEvent logEvent)
        {

            await context.Clients.All.SendAsync("Log", message, logEvent.Level, logEvent.Timestamp.DateTime);
        }
    }
}