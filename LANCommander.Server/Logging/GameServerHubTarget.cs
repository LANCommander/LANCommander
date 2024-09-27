/*
 * using NLog;
using NLog.Config;
using NLog.Targets;

namespace LANCommander.Logging
{
    [Target("GameServerHub")]
    public class GameServerHubTarget : AsyncTaskTarget
    {
        private GameServerHubConnection? Connection;

        [RequiredParameter]
        public string HubUrl { get; set; }

        protected override void InitializeTarget()
        {
            Connection = new GameServerHubConnection(HubUrl);
        }

        protected override async Task WriteAsyncTask(LogEventInfo logEvent, CancellationToken token)
        {
            if (Connection != null && logEvent.Properties.ContainsKey("ServerId"))
                await Connection.Log((Guid)logEvent.Properties["ServerId"], logEvent.FormattedMessage);
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