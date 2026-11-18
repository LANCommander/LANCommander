using LANCommander.Server.Logging;
using LANCommander.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LANCommander.Server.Hubs
{
    /// <summary>
    /// Streams the server's application log ("Log" messages), replaying the tail of today's log file
    /// on connect.
    /// </summary>
    /// <remarks>
    /// For administrators only: logs can contain sensitive details. The server's own log viewer
    /// doesn't connect to it; it subscribes to <see cref="LogStream"/> in-process.
    /// </remarks>
    [Authorize(Roles = RoleService.AdministratorRoleName)]
    public class LoggingHub(LogStream logStream) : Hub
    {
        public override async Task OnConnectedAsync()
        {
            foreach (var entry in await logStream.ReadHistoryAsync())
                await Clients.Caller.SendAsync("Log", entry.Message, entry.Level, entry.Timestamp);

            await Clients.Caller.SendAsync("Log", "Connected to server logging provider!", LogLevel.Information, DateTime.Now);
            await base.OnConnectedAsync();
        }

        public static async Task Log(IHubContext<LoggingHub> context, string message, LogLevel logLevel, DateTime timestamp)
        {
            await context.Clients.All.SendAsync("Log", message, logLevel, timestamp);
        }
    }
}
