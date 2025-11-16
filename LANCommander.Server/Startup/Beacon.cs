using LANCommander.SDK;
using LANCommander.SDK.Interceptors;
using LANCommander.SDK.Services;
using LANCommander.Server.Services;

namespace LANCommander.Server.Startup;

public static class Beacon
{
    public static async Task StartBeaconAsync(this WebApplication app)
    {
        var settingsProvider = app.Services.GetRequiredService<SettingsProvider<Settings.Settings>>();
        var beaconService = app.Services.GetRequiredService<BeaconClient>();
        var interceptor = app.Services.GetRequiredService<IBeaconMessageInterceptor>();
        
        beaconService.Initialize();
        beaconService.AddBeaconMessageInterceptor(interceptor);

        if (settingsProvider.CurrentValue.Server.Beacon.Enabled)
            await beaconService.StartBeaconAsync(settingsProvider.CurrentValue.Server.Beacon.Port, settingsProvider.CurrentValue.Server.Beacon.Address, settingsProvider.CurrentValue.Server.Beacon.Name);
    }
}