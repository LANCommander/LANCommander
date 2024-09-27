using LANCommander.Server.Services;
using Microsoft.AspNetCore.SignalR.Client;

namespace LANCommander.Logging
{
    public class GameServerHubConnection : IAsyncDisposable
    {
        private HubConnection? HubConnection;
        private string HubUrl;

        public GameServerHubConnection(string hubUrl)
        {
            var settings = SettingService.GetSettings();

            HubUrl = hubUrl.Replace("${gdc:PortNumber}", settings.Port.ToString());
        }

        public async Task Log(Guid serverId, string message)
        {
            await EnsureConnection();

            if (HubConnection != null)
                await HubConnection.SendAsync("Log", serverId, message);
        }

        public async Task EnsureConnection()
        {
            if (HubConnection == null)
            {
                HubConnection = new HubConnectionBuilder()
                .WithUrl(HubUrl)
                .Build();

                await HubConnection.StartAsync();
            }
            else if (HubConnection.State == HubConnectionState.Disconnected)
            {
                await HubConnection.StartAsync();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (HubConnection != null)
            {
                try
                {
                    await HubConnection.StopAsync();
                    await HubConnection.DisposeAsync();
                }
                catch (Exception ex)
                {
                    // NLog.Common.InternalLogger?.LogError(ex, "Exception in LoggingHubConnection.DisposeAsync");
                }
                finally
                {
                    HubConnection = null;
                }
            }
        }
    }
}
