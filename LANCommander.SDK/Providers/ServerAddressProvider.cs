using System;
using LANCommander.SDK.Abstractions;

namespace LANCommander.SDK.Providers;

public class ServerAddressProvider(ISettingsProvider settingsProvider) : IServerAddressProvider
{
    public void SetServerAddress(Uri serverAddress)
    {
        settingsProvider.Update(s =>
        {
            s.Authentication.ServerAddress = serverAddress;
        });
    }

    public Uri GetServerAddress() => settingsProvider.CurrentValue.Authentication.ServerAddress;
}