using System.Net;
using LANCommander.SDK.Interceptors;
using LANCommander.SDK.Models;
using LANCommander.Server.Services;

namespace LANCommander.Server.Interceptors;

public class BeaconMessageInterceptor : IBeaconMessageInterceptor
{
    public async Task<BeaconMessage> ExecuteAsync(BeaconMessage message, IPEndPoint interfaceEndPoint)
    {
        if (String.IsNullOrWhiteSpace(message.Address))
        {
            var settings = SettingService.GetSettings();

            if (settings.UseSSL)
                message.Address = $"https://{interfaceEndPoint.Address.ToString()}:{settings.SSLPort}";
            else
                message.Address = $"http://{interfaceEndPoint.Address.ToString()}:{settings.Port}";
        }

        return message;
    }
}