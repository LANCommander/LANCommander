using System.Net;
using LANCommander.SDK.Interceptors;
using LANCommander.SDK.Models;

namespace LANCommander.Server.Services.Interceptors;

public class BeaconMessageInterceptor(SettingsProvider<Settings.Settings> settingsProvider) : IBeaconMessageInterceptor
{
    public async Task<BeaconMessage> ExecuteAsync(BeaconMessage message, IPEndPoint interfaceEndPoint)
    {
        if (String.IsNullOrWhiteSpace(message.Address))
        {
            if (settingsProvider.CurrentValue.Server.Http.UseSSL)
                message.Address = $"https://{interfaceEndPoint.Address.ToString()}:{settingsProvider.CurrentValue.Server.Http.SSLPort}";
            else
                message.Address = $"http://{interfaceEndPoint.Address.ToString()}:{settingsProvider.CurrentValue.Server.Http.Port}";
        }

        return message;
    }
}