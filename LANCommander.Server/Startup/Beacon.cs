using LANCommander.SDK;
using LANCommander.SDK.Services;
using LANCommander.Server.Interceptors;
using LANCommander.Server.Services;

namespace LANCommander.Server.Startup;

public static class Beacon
{
    public static async Task StartBeaconAsync(this WebApplication app)
    {
        var settings = SettingService.GetSettings();
        var beaconService = app.Services.GetRequiredService<BeaconClient>();
        
        beaconService.Initialize();
        beaconService.AddBeaconMessageInterceptor(new BeaconMessageInterceptor());

        if (settings.Beacon.Enabled)
            await beaconService.StartBeaconAsync(settings.Beacon.Port, settings.Beacon.Address, settings.Beacon.Name);
    }
}