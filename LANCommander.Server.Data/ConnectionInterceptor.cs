using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LANCommander.Server.Data
{
    public class ConnectionInterceptor : DbConnectionInterceptor
    {
        private readonly ILogger Logger;

        public ConnectionInterceptor(ILogger logger)
        {
            Logger = logger;
        }

        public override async Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            if (DatabaseContext.ContextTracker.ContainsKey(eventData.ConnectionId))
                DatabaseContext.ContextTracker[eventData.ConnectionId].Stop();

            Logger.LogInformation("DbContext connection closed: {ConnectionId}", eventData.ConnectionId);
            await base.ConnectionClosedAsync(connection, eventData);
        }

        public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
        {
            DatabaseContext.ContextTracker[eventData.ConnectionId] = Stopwatch.StartNew();

            Logger.LogInformation("DbContext connection opened: {ConnectionId}", eventData.ConnectionId);
            await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
        }
    }
}
