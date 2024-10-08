﻿/*
using NLog;
using NLog.Config;
using NLog.Targets;

namespace LANCommander.Logging
{
    [Target("LoggingHub")]
    public class LoggingHubTarget : AsyncTaskTarget
    {
        private LoggingHubConnection? Connection;

        [RequiredParameter]
        public string HubUrl { get; set; }

        protected override void InitializeTarget()
        {
            Connection = new LoggingHubConnection(HubUrl);
        }

        protected override async Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken token)
        {
            if (Connection != null)
                await Connection.Log(Layout.Render(logEvent));
        }

        protected override async void CloseTarget()
        {
            if (Connection != null)
            {
                await Connection.DisposeAsync();
                Connection = null;
            }
        }
    }
}
*/